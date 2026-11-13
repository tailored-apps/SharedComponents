using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TailoredApps.Shared.MediatR.Interfaces.DI;

/// <summary>
/// Contract for registering MediatR pipeline behaviors into the dependency injection container.
/// </summary>
public interface IPipelineRegistration
{
    /// <summary>
    /// Registers the default set of pipeline behaviors: Logging, Validation, Caching, Fallback, and Retry.
    /// </summary>
    void RegisterPipelineBehaviors();

    /// <summary>
    /// Registers the default pipeline behaviors and additionally scans the specified assembly
    /// for implementations of cache policies, fallback handlers, and retryable request configurations.
    /// </summary>
    /// <param name="assembly">The assembly to scan for policy and handler implementations.</param>
    [RequiresUnreferencedCode("Cache policies, fallback handlers and retry settings are found by scanning the assembly with reflection, which trimming breaks. Register them one by one with AddCachePolicy, AddFallbackHandler and AddRetryPolicy on PipelineRegistration.")]
    [RequiresDynamicCode("Cache policies, fallback handlers and retry settings are found by scanning the assembly with reflection, which trimming breaks. Register them one by one with AddCachePolicy, AddFallbackHandler and AddRetryPolicy on PipelineRegistration.")]
    void RegisterPipelineBehaviors(Assembly assembly);
}
