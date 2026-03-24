using MediatR;

namespace TailoredApps.Shared.MediatR.Caching
{
    /// <summary>Marker interfejsu dla żądań MediatR, których wynik może być cachowany.</summary>
    /// <typeparam name="TResponse">Typ odpowiedzi.</typeparam>
    public interface ICachableRequest<TResponse> : IRequest<TResponse>
    {
        /// <summary>Zwraca klucz cache dla tego żądania.</summary>
        string GetCacheKey();
    }
}
