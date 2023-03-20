using System;

namespace TailoredApps.Shared.EntityFramework.Testing
{

    public static class TestObjectFactory
    {
        public static T Create<T>(Action<T> config)
        {
            var obj = (T)Activator.CreateInstance(typeof(T), nonPublic: true);

            config(obj);

            return obj;
        }
    }
}
