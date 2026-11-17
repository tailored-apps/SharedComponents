using FluentValidation;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;

namespace TailoredApps.Demo.Services.Catalog.Validation;

/// <summary>
/// Rejects paging and sorting parameters before the query runs, so that a client mistake is a 400 with the
/// field named, not an exception from the query extensions.
/// </summary>
public class GetProductsValidator : AbstractValidator<GetProducts>
{
    public GetProductsValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1).When(request => request.Page.HasValue);
        RuleFor(request => request.Count).InclusiveBetween(1, GetProducts.MaxPageSize).When(request => request.Count.HasValue);
        RuleFor(request => request.SortField)
            .Must(field => GetProducts.SortableFields.Contains(field, StringComparer.OrdinalIgnoreCase))
            .When(request => !string.IsNullOrEmpty(request.SortField))
            .WithMessage($"Products can be sorted by {string.Join(", ", GetProducts.SortableFields)}.");
    }
}
