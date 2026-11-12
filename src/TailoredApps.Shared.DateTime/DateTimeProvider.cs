namespace TailoredApps.Shared.DateTime;

/// <summary>
/// <see cref="IDateTimeProvider"/> on top of a <see cref="System.TimeProvider"/>. With no argument it reads
/// the system clock; given another provider (the one registered in dependency injection, or a fake one in
/// a test) it reports that provider's time and local time zone.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// Initializes a new instance of <see cref="DateTimeProvider"/> that reads the system clock.
    /// </summary>
    public DateTimeProvider()
        : this(System.TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DateTimeProvider"/> that reads <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The clock and local time zone to report.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="timeProvider"/> is <c>null</c>.</exception>
    public DateTimeProvider(System.TimeProvider timeProvider)
    {
        System.ArgumentNullException.ThrowIfNull(timeProvider);
        TimeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the clock every value is read from. Use it where a <see cref="System.TimeProvider"/> is asked
    /// for, for example to create a timer or a delay that follows the same clock.
    /// </summary>
    public System.TimeProvider TimeProvider { get; }

    /// <inheritdoc/>
    public System.DateTime Now => System.DateTime.SpecifyKind(TimeProvider.GetLocalNow().DateTime, System.DateTimeKind.Local);

    /// <inheritdoc/>
    public System.DateTime UtcNow => TimeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Gets the current date and time in the local time zone of <see cref="TimeProvider"/>, together with
    /// that zone's offset from UTC.
    /// </summary>
    public System.DateTimeOffset OffsetNow => TimeProvider.GetLocalNow();

    /// <summary>
    /// Gets the current date and time in UTC as a <see cref="System.DateTimeOffset"/> with a zero offset.
    /// </summary>
    public System.DateTimeOffset OffsetUtcNow => TimeProvider.GetUtcNow();

    /// <inheritdoc/>
    public System.TimeSpan TimeOfDay => TimeProvider.GetLocalNow().TimeOfDay;

    /// <summary>
    /// Gets the time that has passed since midnight in UTC.
    /// </summary>
    public System.TimeSpan UtcTimeOfDay => TimeProvider.GetUtcNow().TimeOfDay;

    /// <summary>
    /// Gets the time that has passed since midnight in UTC. Misspelled name of <see cref="UtcTimeOfDay"/>,
    /// kept so that existing code compiles.
    /// </summary>
    [System.Obsolete("Use UtcTimeOfDay.")]
    public System.TimeSpan UtcTimeOfDaty => UtcTimeOfDay;

    /// <inheritdoc/>
    public System.DateTime Today => Now.Date;

    /// <inheritdoc/>
    public System.DateTime UtcToday => UtcNow.Date;
}
