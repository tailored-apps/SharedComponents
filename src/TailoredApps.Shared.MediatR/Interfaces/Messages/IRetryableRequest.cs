using MediatR;

namespace TailoredApps.Shared.MediatR.Interfaces.Messages
{
    /// <summary>Marker interfejsu dla żądań MediatR, które obsługują mechanizm retry i circuit breaker.</summary>
    /// <typeparam name="TRequest">Typ żądania MediatR.</typeparam>
    /// <typeparam name="TResponse">Typ odpowiedzi.</typeparam>
    public interface IRetryableRequest<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>Liczba prób ponowienia. Domyślnie 1.</summary>
        int RetryAttempts => 1;

        /// <summary>Opóźnienie między próbami w milisekundach. Domyślnie 250 ms.</summary>
        int RetryDelay => 250;

        /// <summary>Czy używać exponential backoff przy retry. Domyślnie <c>false</c>.</summary>
        bool RetryWithExponentialBackoff => false;

        /// <summary>Liczba wyjątków przed otwarciem circuit breakera. Domyślnie 1.</summary>
        int ExceptionsAllowedBeforeCircuitTrip => 1;
    }
}
