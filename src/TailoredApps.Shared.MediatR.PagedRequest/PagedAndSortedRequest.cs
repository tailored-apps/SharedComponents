using System;
using MediatR;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.MediatR.PagedRequest
{
    /// <summary>
    /// Base MediatR request that combines paging and sorting parameters.
    /// Implement this to pass paged + sorted queries through the MediatR pipeline.
    /// </summary>
    /// <typeparam name="TResponse">The response type returned by the handler (must implement <see cref="IPagedResult{TModel}"/>).</typeparam>
    /// <typeparam name="TQuery">The filter/query object type (must derive from <see cref="QueryBase"/>).</typeparam>
    /// <typeparam name="TModel">The item type contained in the paged result.</typeparam>
    public class PagedAndSortedRequest<TResponse, TQuery, TModel> : IPagedAndSortedRequest<TResponse, TQuery, TModel>, IRequest<TResponse>
        where TQuery : QueryBase
        where TResponse : IPagedResult<TModel>
    {
        /// <summary>Requested page number (1-based). Null means no paging.</summary>
        public int? Page { get; set; }

        /// <summary>Number of items per page. Null means no paging.</summary>
        public int? Count { get; set; }

        /// <summary>Returns <c>true</c> when both <see cref="Page"/> and <see cref="Count"/> are specified.</summary>
        public bool IsPagingSpecified => Page.HasValue && Count.HasValue;

        /// <summary>Name of the field to sort by.</summary>
        public string SortField { get; set; }

        /// <summary>Sort direction (ascending or descending). Null means no sorting.</summary>
        public SortDirection? SortDir { get; set; }

        /// <summary>Returns <c>true</c> when both <see cref="SortField"/> and <see cref="SortDir"/> are specified.</summary>
        public bool IsSortingSpecified => !string.IsNullOrWhiteSpace(SortField) && SortDir.HasValue;

        /// <summary>Filter / query criteria applied to the data set.</summary>
        public TQuery Filter { get; set; }

        /// <summary>
        /// Returns <c>true</c> when <see cref="SortField"/> matches <paramref name="fieldName"/>
        /// using a case-insensitive comparison.
        /// </summary>
        /// <param name="fieldName">The field name to compare against.</param>
        public bool IsSortBy(string fieldName) => string.Equals(SortField, fieldName, StringComparison.InvariantCultureIgnoreCase);
    }
}
