using System.Reflection;

namespace TailoredApps.Shared.MediatR.Interfaces.DI
{
    /// <summary>Kontrakt rejestracji pipeline behaviors MediatR.</summary>
    public interface IPipelineRegistration
    {
        /// <summary>Rejestruje domyślne pipeline behaviors (Logging, Validation, Caching, Fallback, Retry).</summary>
        void RegisterPipelineBehaviors();

        /// <summary>Rejestruje pipeline behaviors i skanuje wskazany assembly w poszukiwaniu polityk cache, fallback i retry.</summary>
        /// <param name="assembly">Assembly do przeskanowania.</param>
        void RegisterPipelineBehaviors(Assembly assembly);
    }
}
