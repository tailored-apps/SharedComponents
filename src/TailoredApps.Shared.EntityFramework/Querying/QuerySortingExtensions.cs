using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using TailoredApps.Shared.EntityFramework.Interfaces;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    /// <summary>
    /// Provides extension methods for applying dynamic sorting to <see cref="IQueryable{T}"/> sequences.
    /// </summary>
    public static class QuerySortingExtensions
    {
        /// <summary>
        /// Applies a single set of sorting parameters to the query.
        /// Returns the original query unchanged if <paramref name="sortingParameters"/> is <c>null</c>
        /// or has no sorting specified.
        /// </summary>
        /// <typeparam name="T">The element type of the query.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="sortingParameters">The sorting parameters to apply.</param>
        /// <returns>The sorted (or original) queryable.</returns>
        public static IQueryable<T> ApplySorting<T>(
            this IQueryable<T> query,
            ISortingParameters sortingParameters)
        {
            return sortingParameters?.IsSortingSpecified == true
                            ? query.OrderBy(GenerateSortQuery(sortingParameters))
                            : query;
        }

        /// <summary>
        /// Applies multiple sets of sorting parameters to the query.
        /// Returns the original query unchanged if no valid sorting parameters are provided.
        /// </summary>
        /// <typeparam name="T">The element type of the query, constrained to <see cref="IModelBase"/>.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="sortingParameters">The collection of sorting parameters to apply in order.</param>
        /// <returns>The sorted (or original) queryable.</returns>
        public static IQueryable<T> ApplySorting<T>(
            this IQueryable<T> query,
            IEnumerable<ISortingParameters> sortingParameters)
            where T : IModelBase
        {
            var parametersSnapshot = sortingParameters?.Where(x => x.IsSortingSpecified)
                                                      .ToList() ?? Enumerable.Empty<ISortingParameters>().ToList();

            return parametersSnapshot.Count > 0
                        ? query.OrderBy(GenerateSortQuery(parametersSnapshot))
                        : query;
        }

        /// <summary>
        /// Applies an optional decorator function to the query (e.g. for custom <c>Include</c> or <c>Where</c> clauses).
        /// Returns the original query unchanged if <paramref name="decorator"/> is <c>null</c>.
        /// </summary>
        /// <typeparam name="T">The element type of the query.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="decorator">An optional function that transforms the query.</param>
        /// <returns>The decorated (or original) queryable.</returns>
        public static IQueryable<T> AdditionOperation<T>(this IQueryable<T> query,
                                                         Func<IQueryable<T>, IQueryable<T>> decorator)
            => decorator?.Invoke(query) ?? query;

        private static string GenerateSortQuery(IEnumerable<ISortingParameters> parameters)
        {
            return string.Join(",", parameters.Where(x => x.IsSortingSpecified)
                .Select(GenerateSortQuery));
        }

        private static string GenerateSortQuery(ISortingParameters sortingParameter)
            => sortingParameter.SortDir == SortDirection.Desc
                ? $"{sortingParameter.SortField} {SortDirection.Desc}"
                : sortingParameter.SortField;

    }
}
