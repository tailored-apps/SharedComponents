using System;
using System.Linq.Expressions;

namespace TailoredApps.Shared.Querying
{
    /// <summary>Maps a sort field from a destination model to a source entity field.</summary>
    /// <typeparam name="TDestination">The type of the destination model (DTO).</typeparam>
    /// <typeparam name="TSource">The type of the source entity.</typeparam>
    public class QueryMap<TDestination, TSource>
    {
        /// <summary>
        /// Initializes a new sort mapping.
        /// </summary>
        /// <param name="destination">An expression pointing to the destination model field.</param>
        /// <param name="source">An expression pointing to the source entity field.</param>
        public QueryMap(Expression<Func<TDestination, object>> destination, Expression<Func<TSource, object>> source)
        {
            Source = source;
            Destination = destination;
        }

        /// <summary>An expression pointing to the source entity field.</summary>
        public Expression<Func<TSource, object>> Source { get; }

        /// <summary>An expression pointing to the destination model field.</summary>
        public Expression<Func<TDestination, object>> Destination { get; }
    }
}
