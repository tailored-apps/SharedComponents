using FluentValidation;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;

namespace TailoredApps.Demo.Services.Catalog.Validation;

public class GetProductValidator : AbstractValidator<GetProduct>
{
    public GetProductValidator() => RuleFor(request => request.Id).GreaterThan(0);
}
