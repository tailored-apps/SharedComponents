using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.Interfaces.Handlers
{
    /// <summary>Handler fallback wywoływany przez <c>FallbackBehavior</c> gdy główny handler rzuci wyjątkiem.</summary>
    /// <typeparam name="TRequest">Typ żądania MediatR.</typeparam>
    /// <typeparam name="TResponse">Typ odpowiedzi.</typeparam>
    public interface IFallbackHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>Obsługuje żądanie w trybie fallback.</summary>
        /// <param name="request">Oryginalne żądanie.</param>
        /// <param name="cancellationToken">Token anulowania.</param>
        Task<TResponse> HandleFallback(TRequest request, CancellationToken cancellationToken);
    }
}
