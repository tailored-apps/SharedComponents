using System;

namespace TailoredApps.Shared.Querying
{
    /// <summary>Base class for a paged and sorted query.</summary>
    /// <typeparam name="TQuery">The type of the query filter.</typeparam>
    public abstract class PagedAndSortedQuery<TQuery> : IPagedAndSortedQuery<TQuery> where TQuery : QueryBase
    {
        /// <summary>Page number (1-based).</summary>
        public int? Page { get; set; }

        /// <summary>Number of items per page.</summary>
        public int? Count { get; set; }

        /// <summary>Indicates whether paging parameters are specified.</summary>
        public bool IsPagingSpecified => Page.HasValue && Count.HasValue;

        /// <summary>The field to sort by.</summary>
        public string SortField { get; set; }

        /// <summary>The sort direction.</summary>
        public SortDirection? SortDir { get; set; }

        /// <summary>Indicates whether sorting parameters are specified.</summary>
        public bool IsSortingSpecified => !string.IsNullOrWhiteSpace(SortField) && SortDir.HasValue;

        /// <summary>The query filter object.</summary>
        public TQuery Filter { get; set; }

        /// <summary>Determines whether the query is sorted by the specified field.</summary>
        /// <param name="fieldName">The name of the field to check.</param>
        /// <returns><c>true</c> if the query is sorted by <paramref name="fieldName"/>; otherwise, <c>false</c>.</returns>
        public bool IsSortBy(string fieldName) => string.Equals(SortField, fieldName, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>Interface for a paged and sorted query.</summary>
    /// <typeparam name="TQuery">The type of the query filter.</typeparam>
    public interface IPagedAndSortedQuery<TQuery> : IQuery<TQuery>, IQueryParameters where TQuery : QueryBase
    {
        /// <summary>Page number (1-based).</summary>
        new int? Page { get; set; }

        /// <summary>Number of items per page.</summary>
        new int? Count { get; set; }

        /// <summary>Indicates whether paging parameters are specified.</summary>
        new bool IsPagingSpecified { get; }

        /// <summary>The field to sort by.</summary>
        new string SortField { get; set; }

        /// <summary>The sort direction.</summary>
        new SortDirection? SortDir { get; set; }

        /// <summary>Indicates whether sorting parameters are specified.</summary>
        new bool IsSortingSpecified { get; }

        /// <summary>The query filter object.</summary>
        new TQuery Filter { get; set; }

        /// <summary>Determines whether the query is sorted by the specified field.</summary>
        /// <param name="fieldName">The name of the field to check.</param>
        /// <returns><c>true</c> if the query is sorted by <paramref name="fieldName"/>; otherwise, <c>false</c>.</returns>
        bool IsSortBy(string fieldName);
    }

    /// <summary>Paging parameters.</summary>
    public interface IPagingParameters
    {
        /// <summary>Page number.</summary>
        int? Page { get; }

        /// <summary>Number of items per page.</summary>
        int? Count { get; }

        /// <summary>Indicates whether paging parameters are specified.</summary>
        bool IsPagingSpecified { get; }
    }

    /// <summary>Sorting parameters.</summary>
    public interface ISortingParameters
    {
        /// <summary>The field to sort by.</summary>
        string SortField { get; }

        /// <summary>The sort direction.</summary>
        SortDirection? SortDir { get; }

        /// <summary>Indicates whether sorting parameters are specified.</summary>
        bool IsSortingSpecified { get; }
    }

    /// <summary>Combined paging and sorting parameters.</summary>
    public interface IQueryParameters : IPagingParameters, ISortingParameters
    {
    }

    /// <summary>Interface for a query with a filter object.</summary>
    /// <typeparam name="T">The type of the filter.</typeparam>
    public interface IQuery<T>
    {
        /// <summary>The query filter object.</summary>
        T Filter { get; set; }
    }

    /// <summary>Interface for a paged MediatR request with a filter and a response model.</summary>
    /// <typeparam name="TResponse">The type of the response (must implement <see cref="IPagedResult{TModel}"/>).</typeparam>
    /// <typeparam name="TQuery">The type of the query filter.</typeparam>
    /// <typeparam name="TModel">The type of the item in the result set.</typeparam>
    public interface IPagedAndSortedRequest<TResponse, TQuery, TModel> : IPagedAndSortedQuery<TQuery>
        where TQuery : QueryBase
        where TResponse : IPagedResult<TModel>
    {
    }
}
