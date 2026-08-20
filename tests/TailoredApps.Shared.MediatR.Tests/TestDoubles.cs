using System.Threading;
using System.Threading.Tasks;
using MediatR;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;

namespace TailoredApps.Shared.MediatR.Tests
{
    /// <summary>Shared MediatR request used by caching pipeline tests.</summary>
    public class CachedRequest : IRequest<string>
    {
        public int Id { get; set; }
    }

    /// <summary>Shared MediatR request used by retry pipeline tests.</summary>
    public class RetryRequest : IRequest<string>
    {
    }

    /// <summary>Shared MediatR request used by fallback pipeline tests.</summary>
    public class FallbackRequest : IRequest<string>
    {
    }

    /// <summary>Shared MediatR request used by logging pipeline tests.</summary>
    public class LoggingRequest : IRequest<string>
    {
        public string Payload { get; set; }
    }

    /// <summary>Shared MediatR request used by validation pipeline tests.</summary>
    public class ValidatedRequest : IRequest<string>
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }

    /// <summary>Concrete cache policy relying entirely on the default interface implementations.</summary>
    public class DefaultCachePolicy : ICachePolicy<CachedRequest, string>
    {
    }

    /// <summary>Concrete retry configuration relying entirely on the default interface implementations.</summary>
    public class DefaultRetryConfig : IRetryableRequest<RetryRequest, string>
    {
    }

    /// <summary>Retry configuration: 2 retry attempts, tiny delay, no exponential backoff.</summary>
    public class TwoAttemptsRetryConfig : IRetryableRequest<RetryRequest, string>
    {
        public int RetryAttempts => 2;

        public int RetryDelay => 1;

        public bool RetryWithExponentialBackoff => false;

        public int ExceptionsAllowedBeforeCircuitTrip => 10;
    }

    /// <summary>Retry configuration: 2 retry attempts, tiny delay, exponential backoff enabled.</summary>
    public class TwoAttemptsExponentialBackoffRetryConfig : IRetryableRequest<RetryRequest, string>
    {
        public int RetryAttempts => 2;

        public int RetryDelay => 1;

        public bool RetryWithExponentialBackoff => true;

        public int ExceptionsAllowedBeforeCircuitTrip => 10;
    }

    /// <summary>Concrete fallback handler used by the assembly-scanning registration tests.</summary>
    public class RecoveringFallbackHandler : IFallbackHandler<FallbackRequest, string>
    {
        public Task<string> HandleFallback(FallbackRequest request, CancellationToken cancellationToken)
            => Task.FromResult("fallback-value");
    }
}
