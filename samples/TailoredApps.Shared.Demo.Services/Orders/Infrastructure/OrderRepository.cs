using ForkForward.Mapper;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Services.Orders.Entities;
using TailoredApps.Demo.Services.Orders.Exceptions;
using TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using Model = TailoredApps.Demo.DataModel.Orders.Model;

namespace TailoredApps.Demo.Services.Orders.Infrastructure;

/// <summary>
/// Orders through the unit of work. No method saves or commits: an order that was added to the context is
/// stored only if the whole request succeeds.
/// </summary>
public class OrderRepository(IUnitOfWork<IShopDataContext> unitOfWork, IMapper mapper) : IOrderRepository
{
    private IShopDataContext Db => unitOfWork.DataProvider;

    public async Task<Model.Order?> FindAsync(string number, CancellationToken cancellationToken)
        => mapper.Map<Model.Order?>(await WithLines().AsNoTracking().SingleOrDefaultAsync(order => order.Number == number, cancellationToken));

    public async Task<Model.Order?> FindByPaymentReferenceAsync(string reference, CancellationToken cancellationToken)
        => mapper.Map<Model.Order?>(await WithLines().AsNoTracking().SingleOrDefaultAsync(order => order.Number == reference || order.PaymentId == reference, cancellationToken));

    public async Task<Model.Order> CreateAsync(string number, string customerEmail, int[] productIds, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        var order = new Order { Number = number, CustomerEmail = customerEmail, Status = OrderStatus.New, CreatedAt = createdAt };
        Db.Orders.Add(order);

        var ids = productIds.Distinct().ToArray();
        var products = await Db.Products.Where(product => ids.Contains(product.Id)).ToListAsync(cancellationToken);
        if (products.Count == 0 || products.Count != ids.Length)
        {
            // The order is already in the context. Throwing makes the host roll the request back.
            throw new UnknownProductsException();
        }

        foreach (var product in products)
        {
            order.Lines.Add(new OrderLine { ProductId = product.Id, Product = product, UnitPrice = product.Price, Quantity = productIds.Count(id => id == product.Id) });
        }

        order.Amount = order.Lines.Sum(line => line.UnitPrice * line.Quantity);
        return mapper.Map<Model.Order>(order);
    }

    public async Task MarkAwaitingPaymentAsync(string number, string provider, string? paymentId, CancellationToken cancellationToken)
    {
        var order = await TrackedAsync(number, cancellationToken);
        order.PaymentProvider = provider;
        order.PaymentId = paymentId;
        order.Status = OrderStatus.AwaitingPayment;
    }

    public async Task MarkPaidAsync(string number, DateTimeOffset paidAt, CancellationToken cancellationToken)
    {
        var order = await TrackedAsync(number, cancellationToken);
        order.Status = OrderStatus.Paid;
        order.PaidAt = paidAt;
    }

    public async Task MarkPaymentRejectedAsync(string number, CancellationToken cancellationToken)
        => (await TrackedAsync(number, cancellationToken)).Status = OrderStatus.PaymentRejected;

    public async Task CancelAsync(string number, CancellationToken cancellationToken)
    {
        var order = await TrackedAsync(number, cancellationToken);
        if (order.Status == OrderStatus.Paid)
            throw new OrderAlreadyPaidException(number);

        order.Status = OrderStatus.Cancelled;
    }

    private IQueryable<Order> WithLines() => Db.Orders.Include(order => order.Lines).ThenInclude(line => line.Product);

    private async Task<Order> TrackedAsync(string number, CancellationToken cancellationToken)
        => await Db.Orders.SingleOrDefaultAsync(order => order.Number == number, cancellationToken) ?? throw new OrderNotFoundException(number);
}
