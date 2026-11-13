using ForkForward.Mapper;
using Model = TailoredApps.Demo.DataModel.Orders.Model;

namespace TailoredApps.Demo.Services.Orders;

/// <summary>
/// What the mapper does by convention and what it is told: the status enum becomes its name, the lines are
/// mapped as a collection, <c>ProductName</c> is flattened from <c>Product.Name</c>, and the line total is
/// computed.
/// </summary>
public class OrdersMappingProfile : Profile
{
    public OrdersMappingProfile()
    {
        CreateMap<Entities.OrderLine, Model.OrderLine>()
            .ForMember(line => line.LineTotal, options => options.MapFrom(line => line.UnitPrice * line.Quantity));

        CreateMap<Entities.Order, Model.Order>();
    }
}
