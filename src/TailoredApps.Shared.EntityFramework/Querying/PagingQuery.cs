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
    public class PagingQuery<T> : IQueryable<T>
    {
        private readonly IPagingParameters pagingParameters;
        public PagingQuery(IQueryable<T> query, IPagingParameters pagingParameters)
        {
            if (pagingParameters == null)
                throw new ArgumentNullException(nameof(pagingParameters));

            if (query == null)
                throw new ArgumentNullException(nameof(query));

            Query = query;
            this.pagingParameters = pagingParameters;

        }

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

        public IQueryable<T> Query { get; private set; }

        public int PageNumber { get; private set; }

        public int PageCount { get; private set; }

        public int TotalCount { get; private set; }

#if DEBUG
        public bool IsMoreDataToFetch => TotalCount < PageCount || InternalPageNumber * PageCount <= TotalCount;
#else
        public bool IsMoreDataToFetch => TotalCount > 0 && (TotalCount < PageCount || InternalPageNumber * PageCount <= TotalCount);
#endif

        public IEnumerator<T> GetEnumerator() => Query.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Expression Expression => Query.Expression;

        public Type ElementType => Query.ElementType;

        public IQueryProvider Provider => Query.Provider;

        private int InternalPageNumber => PageNumber - 1;
    }
}
