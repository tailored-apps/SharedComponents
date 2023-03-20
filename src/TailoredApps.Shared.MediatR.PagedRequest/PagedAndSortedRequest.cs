using MediatR;
using System;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.MediatR.PagedRequest
{
    public class PagedAndSortedRequest<TResponse, TQuery, TModel> : IPagedAndSortedQuery<TQuery>, IRequest<TResponse>
        where TQuery : QueryBase
        where TResponse : IPagedResult<TModel>
    {
        public int? Page { get; set; }
        public int? Count { get; set; }
        public bool IsPagingSpecified => Page.HasValue && Count.HasValue;
        public string SortField { get; set; }
        public SortDirection? SortDir { get; set; }
        public bool IsSortingSpecified => !string.IsNullOrWhiteSpace(SortField) && SortDir.HasValue;
        public TQuery Filter { get; set; }
        public bool IsSortBy(string fieldName) => string.Equals(SortField, fieldName, StringComparison.InvariantCultureIgnoreCase);
    }
}
