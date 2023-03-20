using MediatR;

namespace TailoredApps.Shared.MediatR.Caching
{
    public interface ICachableRequest<TResponse> : IRequest<TResponse>
    {
        string GetCacheKey();
    }
}
