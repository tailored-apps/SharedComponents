using System;
using System.Linq.Expressions;

namespace TailoredApps.Shared.Querying
{
    public class QueryMap<TDestination, TSource>
    {
        public QueryMap(Expression<Func<TDestination, object>> destination, Expression<Func<TSource, object>> source)
        {
            Source = source;
            Destination = destination;
        }

        public Expression<Func<TSource, object>> Source { get; }
        public Expression<Func<TDestination, object>> Destination { get; }
    }
}
