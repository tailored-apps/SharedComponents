using System;

namespace TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class HandleExceptionAttribute : Attribute
    {
        public HandleExceptionAttribute()
        {

        }
    }
}
