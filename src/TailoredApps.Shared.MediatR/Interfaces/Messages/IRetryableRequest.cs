using MediatR;

namespace TailoredApps.Shared.MediatR.Interfaces.Messages
{
    public interface IRetryableRequest<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        int RetryAttempts => 1;

        int RetryDelay => 250;

        bool RetryWithExponentialBackoff => false;

        int ExceptionsAllowedBeforeCircuitTrip => 1;

    }
}
