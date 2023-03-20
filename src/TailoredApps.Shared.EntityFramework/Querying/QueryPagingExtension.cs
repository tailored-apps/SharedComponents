using System;
using System.Linq;
using System.Threading.Tasks;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    public static class QueryPagingExtension
    {
        public static PagedResult<T> Paging<T>(this IQueryable<T> query, IPagingParameters paging)
        {
            var pagingQuery = new PagingQuery<T>(query, paging).GetPagingQuery();

            return new PagedResult<T>(pagingQuery).GetPagedResult();
        }

        public static async Task<PagedResult<T>> PagingAsync<T>(this IQueryable<T> query, IPagingParameters paging)
        {
            var pagingQuery = await new PagingQuery<T>(query, paging).GetPagingQueryAsync();
            var result = await new PagedResult<T>(pagingQuery).GetPagedResultAsync();
            return result;
        }

        public static PagedResult<TDst> Project<TSrc, TDst>(this PagedResult<TSrc> pagedResult, Func<TSrc, TDst> projector)
        {
            if (pagedResult == null) throw new ArgumentNullException(nameof(pagedResult));
            if (projector == null) throw new ArgumentNullException(nameof(projector));

            var destinationModels = pagedResult.Results.Select(projector).ToList();

            return new PagedResult<TDst>(destinationModels, pagedResult.Count);
        }
    }
}
