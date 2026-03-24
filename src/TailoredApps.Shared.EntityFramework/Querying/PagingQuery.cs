using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    /// <summary>
    /// Wraps an <see cref="IQueryable{T}"/> with paging information, applying skip/take logic
    /// based on the provided <see cref="IPagingParameters"/>. Implements <see cref="IQueryable{T}"/>
    /// so it can be consumed directly by EF Core materialization methods.
    /// </summary>
    /// <typeparam name="T">The type of elements in the query.</typeparam>
    public class PagingQuery<T> : IQueryable<T>
    {
        private readonly IPagingParameters pagingParameters;

        /// <summary>
        /// Initializes a new instance of <see cref="PagingQuery{T}"/>.
        /// </summary>
        /// <param name="query">The source queryable to page.</param>
        /// <param name="pagingParameters">The paging parameters (page number and page size).</param>
        public PagingQuery(IQueryable<T> query, IPagingParameters pagingParameters)
        {
            if (pagingParameters == null)
                throw new ArgumentNullException(nameof(pagingParameters));

            if (query == null)
                throw new ArgumentNullException(nameof(query));

            Query = query;
            this.pagingParameters = pagingParameters;

        }

        /// <summary>
        /// Asynchronously counts total records and applies skip/take to <see cref="Query"/>
        /// when paging parameters are specified.
        /// </summary>
        /// <returns>This instance with paging applied.</returns>
        public async Task<PagingQuery<T>> GetPagingQueryAsync()
        {

            TotalCount = await Query.CountAsync();
            if (pagingParameters.IsPagingSpecified)
            {
                PageCount = pagingParameters.Count.Value;
                PageNumber = pagingParameters.Page.Value;
                Query = Query.Skip(InternalPageNumber * PageCount).Take(PageCount);
            }
            return this;
        }

        /// <summary>
        /// Synchronously counts total records and applies skip/take to <see cref="Query"/>
        /// when paging parameters are specified.
        /// </summary>
        /// <returns>This instance with paging applied.</returns>
        public PagingQuery<T> GetPagingQuery()
        {

            TotalCount = Query.Count();
            if (pagingParameters.IsPagingSpecified)
            {
                PageCount = pagingParameters.Count.Value;
                PageNumber = pagingParameters.Page.Value;
                Query = Query.Skip(InternalPageNumber * PageCount).Take(PageCount);
            }
            return this;
        }

        /// <summary>
        /// Gets the underlying queryable with optional skip/take applied.
        /// </summary>
        public IQueryable<T> Query { get; private set; }

        /// <summary>
        /// Gets the 1-based page number requested.
        /// </summary>
        public int PageNumber { get; private set; }

        /// <summary>
        /// Gets the number of items per page.
        /// </summary>
        public int PageCount { get; private set; }

        /// <summary>
        /// Gets the total number of records in the unpaged query.
        /// </summary>
        public int TotalCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether there are more records to fetch for the requested page.
        /// </summary>
#if DEBUG
        public bool IsMoreDataToFetch => TotalCount < PageCount || InternalPageNumber * PageCount <= TotalCount;
#else
        public bool IsMoreDataToFetch => TotalCount > 0 && (TotalCount < PageCount || InternalPageNumber * PageCount <= TotalCount);
#endif

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() => Query.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public Expression Expression => Query.Expression;

        /// <inheritdoc/>
        public Type ElementType => Query.ElementType;

        /// <inheritdoc/>
        public IQueryProvider Provider => Query.Provider;

        private int InternalPageNumber => PageNumber - 1;
    }
}
