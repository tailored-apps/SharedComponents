using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TailoredApps.Shared.DateTime;

/// <summary>
/// Dependency-injection registration of the clock.
/// </summary>
public static class DateTimeProviderExtensions
{
    /// <summary>
    /// Registers <see cref="IDateTimeProvider"/> and the <see cref="System.TimeProvider"/> it reads, both as
    /// singletons.
    /// </summary>
    /// <remarks>
    /// A <see cref="System.TimeProvider"/> or an <see cref="IDateTimeProvider"/> that is already registered is
    /// kept, so a host (or a test) can supply its own clock before or after this call:
    /// <see cref="IDateTimeProvider"/> always reads the <see cref="System.TimeProvider"/> the container resolves.
    /// </remarks>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    public static IServiceCollection AddDateTimeProvider(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(System.TimeProvider.System);
        services.TryAddSingleton<IDateTimeProvider>(provider => new DateTimeProvider(provider.GetRequiredService<System.TimeProvider>()));
        return services;
    }
}
