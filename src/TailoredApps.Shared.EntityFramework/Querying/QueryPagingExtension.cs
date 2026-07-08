using System;
using System.Linq;
using System.Threading.Tasks;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    /// <summary>
    /// Provides extension methods for applying paging to <see cref="IQueryable{T}"/> sequences
    /// and projecting <see cref="PagedResult{T}"/> instances to a different type.
    /// </summary>
    public static class QueryPagingExtension
    {
        /// <summary>
        /// Synchronously applies paging to the query and returns a <see cref="PagedResult{T}"/>.
        /// </summary>
        /// <typeparam name="T">The element type of the query.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="paging">The paging parameters specifying page number and page size.</param>
        /// <returns>A <see cref="PagedResult{T}"/> containing the requested page and total count.</returns>
        public static PagedResult<T> Paging<T>(this IQueryable<T> query, IPagingParameters paging)
        {
            var pagingQuery = new PagingQuery<T>(query, paging).GetPagingQuery();

            return new PagedResult<T>(pagingQuery).GetPagedResult();
        }

        /// <summary>
        /// Asynchronously applies paging to the query and returns a <see cref="PagedResult{T}"/>.
        /// </summary>
        /// <typeparam name="T">The element type of the query.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="paging">The paging parameters specifying page number and page size.</param>
        /// <returns>
        /// A task that resolves to a <see cref="PagedResult{T}"/> containing the requested page and total count.
        /// </returns>
        public static async Task<PagedResult<T>> PagingAsync<T>(this IQueryable<T> query, IPagingParameters paging)
        {
            var pagingQuery = await new PagingQuery<T>(query, paging).GetPagingQueryAsync();
            var result = await new PagedResult<T>(pagingQuery).GetPagedResultAsync();
            return result;
        }

        /// <summary>
        /// Projects the items of a <see cref="PagedResult{TSrc}"/> to a new type <typeparamref name="TDst"/>
        /// using the provided projector function, preserving the total count.
        /// </summary>
        /// <typeparam name="TSrc">The source item type.</typeparam>
        /// <typeparam name="TDst">The destination item type.</typeparam>
        /// <param name="pagedResult">The source paged result to project.</param>
        /// <param name="projector">A function that maps each source item to the destination type.</param>
        /// <returns>A new <see cref="PagedResult{TDst}"/> with projected items and the original count.</returns>
        public static PagedResult<TDst> Project<TSrc, TDst>(this PagedResult<TSrc> pagedResult, Func<TSrc, TDst> projector)
        {
            if (pagedResult == null) throw new ArgumentNullException(nameof(pagedResult));
            if (projector == null) throw new ArgumentNullException(nameof(projector));

            var destinationModels = pagedResult.Results.Select(projector).ToList();

            return new PagedResult<TDst>(destinationModels, pagedResult.Count);
        }
    }
}
