using System;
using System.Linq.Expressions;

namespace TailoredApps.Shared.Querying
{
    /// <summary>Mapowanie pola sortowania z modelu docelowego na pole źródłowe.</summary>
    /// <typeparam name="TDestination">Typ modelu docelowego (DTO).</typeparam>
    /// <typeparam name="TSource">Typ encji źródłowej.</typeparam>
    public class QueryMap<TDestination, TSource>
    {
        /// <summary>
        /// Inicjalizuje nowe mapowanie sortowania.
        /// </summary>
        /// <param name="destination">Wyrażenie na pole modelu docelowego.</param>
        /// <param name="source">Wyrażenie na pole encji źródłowej.</param>
        public QueryMap(Expression<Func<TDestination, object>> destination, Expression<Func<TSource, object>> source)
        {
            Source = source;
            Destination = destination;
        }

        /// <summary>Wyrażenie wskazujące na pole encji źródłowej.</summary>
        public Expression<Func<TSource, object>> Source { get; }

        /// <summary>Wyrażenie wskazujące na pole modelu docelowego.</summary>
        public Expression<Func<TDestination, object>> Destination { get; }
    }
}
