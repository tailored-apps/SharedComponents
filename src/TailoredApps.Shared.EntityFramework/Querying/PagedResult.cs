using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    public class PagedResult<T> : IPagedResult<T>
    {
        private static readonly IEnumerable<T> EmptyList = Enumerable.Empty<T>();
        private readonly PagingQuery<T> pagingQuery;
        public PagedResult(PagingQuery<T> pagingQuery)
        {
            if (pagingQuery == null) throw new ArgumentNullException(nameof(pagingQuery));
            this.pagingQuery = pagingQuery;
        }

        public PagedResult(List<T> results, int? count = null)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
            Count = count ?? results.Count;
        }

        public async Task<PagedResult<T>> GetPagedResultAsync()
        {
            Results = pagingQuery.IsMoreDataToFetch ? await pagingQuery.ToListAsync() : EmptyList.ToList();
            Count = pagingQuery.TotalCount > 0 ? pagingQuery.TotalCount : Results.Count;
            return this;
        }

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

        public ICollection<T> Results { get; set; }

        public int Count { get; set; }

        public static PagedResult<T> Empty => new PagedResult<T>();
    }
}
