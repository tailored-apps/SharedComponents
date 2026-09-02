using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Text.RegularExpressions;
using TailoredApps.Shared.EntityFramework.Interfaces;
using TailoredApps.Shared.Querying;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    /// <summary>
    /// Provides extension methods for applying dynamic sorting to <see cref="IQueryable{T}"/> sequences.
    /// </summary>
    /// <remarks>
    /// Sort field names usually come straight from the HTTP request. To keep a client from ordering by
    /// navigation properties (<c>Owner.PasswordHash</c>), by several columns at once or by expressions,
    /// every field is validated as a plain identifier before it reaches Dynamic LINQ, and callers can
    /// additionally restrict sorting to an explicit allow-list of property names.
    /// </remarks>
    public static class QuerySortingExtensions
    {
        private static readonly Regex IdentifierPattern = new("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.Compiled);

        /// <summary>
        /// Applies a single set of sorting parameters to the query.
        /// Returns the original query unchanged if <paramref name="sortingParameters"/> is <c>null</c>
        /// or has no sorting specified.
        /// </summary>
        /// <typeparam name="T">The element type of the query.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="sortingParameters">The sorting parameters to apply.</param>
        /// <returns>The sorted (or original) queryable.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the sort field is not a simple property name or does not exist on <typeparamref name="T"/>.
        /// </exception>
        public static IQueryable<T> ApplySorting<T>(
            this IQueryable<T> query,
            ISortingParameters sortingParameters)
            => query.ApplySorting(sortingParameters, allowedSortFields: null);

        /// <summary>
        /// Applies a single set of sorting parameters to the query, accepting only the fields listed in
        /// <paramref name="allowedSortFields"/> (compared case-insensitively).
        /// </summary>
        /// <typeparam name="T">The element type of the query.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="sortingParameters">The sorting parameters to apply.</param>
        /// <param name="allowedSortFields">
        /// Property names the client may sort by. Pass <c>null</c> to allow any top-level property.
        /// </param>
        /// <returns>The sorted (or original) queryable.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the sort field is not a simple property name, is not allowed, or does not exist.
        /// </exception>
        public static IQueryable<T> ApplySorting<T>(
            this IQueryable<T> query,
            ISortingParameters sortingParameters,
            IEnumerable<string> allowedSortFields)
        {
            if (sortingParameters?.IsSortingSpecified != true)
                return query;

            ValidateSortField(sortingParameters.SortField, allowedSortFields);
            return OrderBySafely(query, GenerateSortQuery(sortingParameters));
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
            => query.ApplySorting(sortingParameters, allowedSortFields: null);

        /// <summary>
        /// Applies multiple sets of sorting parameters to the query, accepting only the fields listed in
        /// <paramref name="allowedSortFields"/> (compared case-insensitively).
        /// </summary>
        /// <typeparam name="T">The element type of the query, constrained to <see cref="IModelBase"/>.</typeparam>
        /// <param name="query">The source queryable.</param>
        /// <param name="sortingParameters">The collection of sorting parameters to apply in order.</param>
        /// <param name="allowedSortFields">
        /// Property names the client may sort by. Pass <c>null</c> to allow any top-level property.
        /// </param>
        /// <returns>The sorted (or original) queryable.</returns>
        public static IQueryable<T> ApplySorting<T>(
            this IQueryable<T> query,
            IEnumerable<ISortingParameters> sortingParameters,
            IEnumerable<string> allowedSortFields)
            where T : IModelBase
        {
            var parametersSnapshot = sortingParameters?.Where(x => x.IsSortingSpecified)
                                                      .ToList() ?? Enumerable.Empty<ISortingParameters>().ToList();

            if (parametersSnapshot.Count == 0)
                return query;

            foreach (var parameter in parametersSnapshot)
                ValidateSortField(parameter.SortField, allowedSortFields);

            return OrderBySafely(query, GenerateSortQuery(parametersSnapshot));
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="sortField"/> is a plain identifier (letters, digits and
        /// underscore, not starting with a digit) - i.e. a single top-level property name without member access,
        /// commas, whitespace or operators.
        /// </summary>
        /// <param name="sortField">The field name supplied by the caller.</param>
        public static bool IsSimpleSortField(string sortField)
            => !string.IsNullOrWhiteSpace(sortField) && IdentifierPattern.IsMatch(sortField);

        private static void ValidateSortField(string sortField, IEnumerable<string> allowedSortFields)
        {
            if (!IsSimpleSortField(sortField))
                throw new ArgumentException("Sort field must be a single property name.", nameof(sortField));

            if (allowedSortFields != null
                && !allowedSortFields.Contains(sortField, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Sorting by '{sortField}' is not allowed.", nameof(sortField));
        }

        private static IQueryable<T> OrderBySafely<T>(IQueryable<T> query, string ordering)
        {
            try
            {
                return query.OrderBy(ordering);
            }
            catch (ParseException ex)
            {
                // Do not echo Dynamic LINQ's message: it enumerates the entity's type and property names.
                throw new ArgumentException("Unsupported sort field.", nameof(ordering), ex);
            }
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
