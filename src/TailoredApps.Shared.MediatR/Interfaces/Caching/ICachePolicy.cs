using MediatR;
using System;
using System.Linq;

namespace TailoredApps.Shared.MediatR.Interfaces.Caching
{
    /// <summary>
    /// Polityka cache'owania dla żądania MediatR.
    /// Używa domyślnych implementacji interfejsów (C# 8.0).
    /// </summary>
    /// <typeparam name="TRequest">Typ żądania MediatR.</typeparam>
    /// <typeparam name="TResponse">Typ odpowiedzi.</typeparam>
    public interface ICachePolicy<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>Absolutna data wygaśnięcia. Domyślnie <c>null</c> (brak ograniczenia).</summary>
        DateTime? AbsoluteExpiration => null;

        /// <summary>Absolutna data wygaśnięcia relative to now. Domyślnie 5 minut.</summary>
        TimeSpan? AbsoluteExpirationRelativeToNow => TimeSpan.FromMinutes(5);

        /// <summary>Sliding expiration. Domyślnie 30 sekund.</summary>
        TimeSpan? SlidingExpiration => TimeSpan.FromSeconds(30);

        /// <summary>Generuje klucz cache na podstawie typu żądania i jego właściwości.</summary>
        /// <param name="request">Żądanie MediatR.</param>
        string GetCacheKey(TRequest request)
        {
            var r = new { request };
            var props = r.request.GetType().GetProperties().Select(pi => $"{pi.Name}:{pi.GetValue(r.request, null)}");
            return $"{typeof(TRequest).FullName}{{{string.Join(",", props)}}}";
        }
    }
}
