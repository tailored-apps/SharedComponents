using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    /// <summary>
    /// Represents a single page of query results along with the total record count.
    /// Can be constructed from a <see cref="PagingQuery{T}"/> for deferred execution
    /// or directly from an in-memory list.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged result.</typeparam>
    public class PagedResult<T> : IPagedResult<T>
    {
        private static readonly IEnumerable<T> EmptyList = Enumerable.Empty<T>();
        private readonly PagingQuery<T> pagingQuery;

        /// <summary>
        /// Initializes a new instance of <see cref="PagedResult{T}"/> backed by a <see cref="PagingQuery{T}"/>.
        /// Call <see cref="GetPagedResultAsync"/> or <see cref="GetPagedResult"/> to execute the query.
        /// </summary>
        /// <param name="pagingQuery">The paging query that will provide the results.</param>
        public PagedResult(PagingQuery<T> pagingQuery)
        {
            if (pagingQuery == null) throw new ArgumentNullException(nameof(pagingQuery));
            this.pagingQuery = pagingQuery;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="PagedResult{T}"/> from an already-materialized list.
        /// </summary>
        /// <param name="results">The list of items for this page.</param>
        /// <param name="count">
        /// The total number of records across all pages. Defaults to the size of <paramref name="results"/>
        /// when not provided.
        /// </param>
        public PagedResult(List<T> results, int? count = null)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
            Count = count ?? results.Count;
        }

        /// <summary>
        /// Asynchronously executes the underlying <see cref="PagingQuery{T}"/> and populates
        /// <see cref="Results"/> and <see cref="Count"/>.
        /// </summary>
        /// <returns>This instance with <see cref="Results"/> and <see cref="Count"/> populated.</returns>
        public async Task<PagedResult<T>> GetPagedResultAsync()
        {
            Results = pagingQuery.IsMoreDataToFetch ? await pagingQuery.ToListAsync() : EmptyList.ToList();
            Count = pagingQuery.TotalCount > 0 ? pagingQuery.TotalCount : Results.Count;
            return this;
        }

        /// <summary>
        /// Synchronously executes the underlying <see cref="PagingQuery{T}"/> and populates
        /// <see cref="Results"/> and <see cref="Count"/>.
        /// </summary>
        /// <returns>This instance with <see cref="Results"/> and <see cref="Count"/> populated.</returns>
        public PagedResult<T> GetPagedResult()
        {
            if (pagingQuery == null) throw new ArgumentNullException(nameof(pagingQuery));


            Results = pagingQuery.IsMoreDataToFetch ? pagingQuery.ToList() : EmptyList.ToList();
            Count = pagingQuery.TotalCount > 0 ? pagingQuery.TotalCount : Results.Count;
            return this;
        }

        private PagedResult()
        {
            Results = EmptyList.ToList();
        }

        /// <summary>
        /// Gets or sets the collection of items for the current page.
        /// </summary>
        public ICollection<T> Results { get; set; }

        /// <summary>
        /// Gets or sets the total number of records across all pages.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets an empty <see cref="PagedResult{T}"/> with no results and a count of zero.
        /// </summary>
        public static PagedResult<T> Empty => new PagedResult<T>();
    }
}
