using FluentValidation;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;

namespace TailoredApps.Demo.Services.Catalog.Validation;

public class AddProductValidator : AbstractValidator<AddProduct>
{
    public AddProductValidator()
    {
        RuleFor(product => product.Name).NotEmpty().MaximumLength(100);
        RuleFor(product => product.Category).NotEmpty().MaximumLength(50);
        RuleFor(product => product.Price).GreaterThan(0);
    }
}
