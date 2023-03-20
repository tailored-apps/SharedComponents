using System;

namespace TailoredApps.Shared.Querying
{
    public abstract class PagedAndSortedQuery<TQuery> : IPagedAndSortedQuery<TQuery> where TQuery : QueryBase
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

    public interface IPagedAndSortedQuery<TQuery> : IQuery<TQuery>, IQueryParameters where TQuery : QueryBase
    {
        int? Page { get; set; }
        int? Count { get; set; }
        bool IsPagingSpecified { get; }
        string SortField { get; set; }
        SortDirection? SortDir { get; set; }
        bool IsSortingSpecified { get; }
        TQuery Filter { get; set; }
        bool IsSortBy(string fieldName);
    }
    public interface IPagingParameters
    {
        int? Page { get; }
        int? Count { get; }
        bool IsPagingSpecified { get; }
    }

    public interface ISortingParameters
    {
        string SortField { get; }
        SortDirection? SortDir { get; }
        bool IsSortingSpecified { get; }
    }

    public interface IQueryParameters : IPagingParameters, ISortingParameters
    {
    }

    public interface IQuery<T>
    {
        T Filter { get; set; }
    }
}
