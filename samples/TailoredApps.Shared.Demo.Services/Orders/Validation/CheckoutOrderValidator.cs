using FluentValidation;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;

namespace TailoredApps.Demo.Services.Orders.Validation;

public class CheckoutOrderValidator : AbstractValidator<CheckoutOrder>
{
    public CheckoutOrderValidator()
    {
        RuleFor(checkout => checkout.Number).NotEmpty();
        RuleFor(checkout => checkout.Provider).NotEmpty();
    }
}
