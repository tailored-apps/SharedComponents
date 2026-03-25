using System;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes
{
    /// <summary>
    /// Marks a controller action (or an entire controller) as eligible for automatic exception
    /// and model-state validation handling by <see cref="Filters.HandleExceptionFilterAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class HandleExceptionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="HandleExceptionAttribute"/>.
        /// </summary>
        public HandleExceptionAttribute()
        {

        }
    }
}
