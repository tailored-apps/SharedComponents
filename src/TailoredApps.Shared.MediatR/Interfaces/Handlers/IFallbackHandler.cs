using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.Interfaces.Handlers
{
    public interface IFallbackHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> HandleFallback(TRequest request, CancellationToken cancellationToken);
    }
}