using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using TailoredApps.Shared.EntityFramework.Interfaces;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    public static class QuerySortingExtensions
    {
        public static IQueryable<T> ApplySorting<T>(
            this IQueryable<T> query,
            ISortingParameters sortingParameters)
        {
            return sortingParameters?.IsSortingSpecified == true
                            ? query.OrderBy(GenerateSortQuery(sortingParameters))
                            : query;
        }

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
