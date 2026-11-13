using FluentValidation;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;

namespace TailoredApps.Demo.Services.Orders.Validation;

public class CreateOrderValidator : AbstractValidator<CreateOrder>
{
    public CreateOrderValidator()
    {
        RuleFor(order => order.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(order => order.ProductIds).NotEmpty();
    }
}
