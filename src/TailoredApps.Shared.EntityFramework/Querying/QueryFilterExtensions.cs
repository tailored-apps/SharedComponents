using System;
using System.Linq;
using System.Linq.Expressions;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Shared.EntityFramework.Querying
{
    /// <summary>
    /// Provides extension methods for filtering <see cref="IQueryable{T}"/> sequences of
    /// <see cref="IModelBase"/> entities.
    /// </summary>
    public static class QueryFilterExtensions
    {
        /// <summary>
        /// Applies an optional filter expression to the query.
        /// If <paramref name="filter"/> is <c>null</c>, the original query is returned unchanged.
        /// </summary>
        /// <typeparam name="T">The entity type, constrained to <see cref="IModelBase"/>.</typeparam>
        /// <param name="query">The source queryable to filter.</param>
        /// <param name="filter">
        /// The predicate expression to apply, or <c>null</c> to skip filtering.
        /// </param>
        /// <returns>The filtered (or original) queryable.</returns>
        public static IQueryable<T> Filter<T>(
            this IQueryable<T> query,
            Expression<Func<T, bool>> filter)
            where T : IModelBase
        {
            if (filter == null)
                return query;

            return query.Where(filter);
        }
    }

}
