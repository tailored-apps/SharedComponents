using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        /// <summary>
        /// Default upper bound for the page size when none is passed to the constructor.
        /// A request such as <c>count=2147483647</c> would otherwise materialise a whole table.
        /// </summary>
        public const int DefaultMaxPageSize = 1000;

        private static int globalMaxPageSize = DefaultMaxPageSize;

        /// <summary>
        /// Process-wide upper bound for the page size used by every <see cref="PagingQuery{T}"/> created
        /// without an explicit limit. Must be at least 1.
        /// </summary>
        public static int MaxPageSize
        {
            get => globalMaxPageSize;
            set => globalMaxPageSize = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxPageSize must be at least 1.");
        }

        private readonly IPagingParameters pagingParameters;
        private readonly int maxPageSize;

        /// <summary>
        /// Initializes a new instance of <see cref="PagingQuery{T}"/> using <see cref="MaxPageSize"/>
        /// as the page-size limit.
        /// </summary>
        /// <param name="query">The source queryable to page.</param>
        /// <param name="pagingParameters">The paging parameters (page number and page size).</param>
        public PagingQuery(IQueryable<T> query, IPagingParameters pagingParameters)
            : this(query, pagingParameters, MaxPageSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="PagingQuery{T}"/> with an explicit page-size limit.
        /// </summary>
        /// <param name="query">The source queryable to page.</param>
        /// <param name="pagingParameters">The paging parameters (page number and page size).</param>
        /// <param name="maxPageSize">The largest page size a caller may request (at least 1).</param>
        public PagingQuery(IQueryable<T> query, IPagingParameters pagingParameters, int maxPageSize)
        {
            if (pagingParameters == null)
                throw new ArgumentNullException(nameof(pagingParameters));

            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (maxPageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(maxPageSize), maxPageSize, "maxPageSize must be at least 1.");

            Query = query;
            this.pagingParameters = pagingParameters;
            this.maxPageSize = maxPageSize;
        }

        /// <summary>
        /// Asynchronously counts total records and applies skip/take to <see cref="Query"/>
        /// when paging parameters are specified.
        /// </summary>
        /// <returns>This instance with paging applied.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the page number is below 1 or the page size is outside 1..max page size.
        /// </exception>
        public async Task<PagingQuery<T>> GetPagingQueryAsync()
        {
            var skip = pagingParameters.IsPagingSpecified ? ValidateAndComputeSkip() : 0;
            TotalCount = await Query.CountAsync();
            if (pagingParameters.IsPagingSpecified)
            {
                Query = Query.Skip(skip).Take(PageCount);
            }
            return this;
        }

        /// <summary>
        /// Synchronously counts total records and applies skip/take to <see cref="Query"/>
        /// when paging parameters are specified.
        /// </summary>
        /// <returns>This instance with paging applied.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the page number is below 1 or the page size is outside 1..max page size.
        /// </exception>
        public PagingQuery<T> GetPagingQuery()
        {
            var skip = pagingParameters.IsPagingSpecified ? ValidateAndComputeSkip() : 0;
            TotalCount = Query.Count();
            if (pagingParameters.IsPagingSpecified)
            {
                Query = Query.Skip(skip).Take(PageCount);
            }
            return this;
        }

        /// <summary>
        /// Validates the requested page/size, stores them and returns the number of rows to skip.
        /// </summary>
        private int ValidateAndComputeSkip()
        {
            var page = pagingParameters.Page.Value;
            var size = pagingParameters.Count.Value;

            if (page < 1)
                throw new ArgumentOutOfRangeException(nameof(pagingParameters.Page), page, "Page number must be at least 1.");

            if (size < 1 || size > maxPageSize)
                throw new ArgumentOutOfRangeException(nameof(pagingParameters.Count), size, $"Page size must be between 1 and {maxPageSize}.");

            var skip = (long)(page - 1) * size;
            if (skip > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(pagingParameters.Page), page, "Page number is too large.");

            PageCount = size;
            PageNumber = page;
            return (int)skip;
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
