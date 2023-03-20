using System.Reflection;

namespace TailoredApps.Shared.MediatR.Interfaces.DI
{
    public interface IPipelineRegistration
    {
        void RegisterPipelineBehaviors();
        void RegisterPipelineBehaviors(Assembly assembly);
    }
}
