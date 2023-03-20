using System;
using System.Linq;
using System.Linq.Expressions;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Shared.EntityFramework.Querying
{

    public static class QueryFilterExtensions
    {
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
