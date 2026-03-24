using System;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.Interfaces.Caching
{
    /// <summary>Wrapper nad <c>IDistributedCache</c> z metodami odczytu i zapisu z serializacją JSON.</summary>
    public interface ICache
    {
        /// <summary>Pobiera i deserializuje wartość z cache.</summary>
        /// <typeparam name="T">Typ deserializowanego obiektu.</typeparam>
        /// <param name="cacheKey">Klucz cache.</param>
        /// <param name="cancellationToken">Token anulowania.</param>
        Task<T> GetAsync<T>(string cacheKey, CancellationToken cancellationToken);

        /// <summary>Serializuje i zapisuje wartość w cache z opcjonalnymi politykami wygaśnięcia.</summary>
        /// <typeparam name="TResponse">Typ serializowanego obiektu.</typeparam>
        /// <param name="cacheKey">Klucz cache.</param>
        /// <param name="response">Obiekt do zapisania.</param>
        /// <param name="slidingExpiration">Sliding expiration (opcjonalnie).</param>
        /// <param name="absoluteExpiration">Absolutna data wygaśnięcia (opcjonalnie).</param>
        /// <param name="absoluteExpirationRelativeToNow">Absolutna data wygaśnięcia relative to now (opcjonalnie).</param>
        /// <param name="cancellationToken">Token anulowania.</param>
        Task SetAsync<TResponse>(string cacheKey, TResponse response, TimeSpan? slidingExpiration, DateTime? absoluteExpiration, TimeSpan? absoluteExpirationRelativeToNow, CancellationToken cancellationToken);
    }
}
