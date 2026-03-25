using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.Interfaces.Handlers
{
    /// <summary>
    /// Fallback handler invoked by <see cref="TailoredApps.Shared.MediatR.PipelineBehaviours.FallbackBehavior{TRequest,TResponse}"/>
    /// when the primary MediatR handler throws an exception. Implement this interface to provide
    /// a graceful alternative response instead of propagating the error.
    /// </summary>
    /// <typeparam name="TRequest">The type of the MediatR request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IFallbackHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request in fallback mode, providing an alternative response when the
        /// primary handler has failed.
        /// </summary>
        /// <param name="request">The original MediatR request that triggered the fallback.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A fallback response of type <typeparamref name="TResponse"/>.</returns>
        Task<TResponse> HandleFallback(TRequest request, CancellationToken cancellationToken);
    }
}
