namespace TailoredApps.Shared.DateTime;

/// <summary>
/// <see cref="IDateTimeProvider"/> on top of a <see cref="System.TimeProvider"/>. With no argument it reads
/// the system clock; given another provider (a fake one in a test, or the one registered in dependency
/// injection) it reports that provider's time and local time zone.
/// </summary>
[System.Obsolete(ObsoleteMessage, DiagnosticId = ObsoleteDiagnosticId)]
public class DateTimeProvider : IDateTimeProvider
{
    /// <summary>The diagnostic reported where <see cref="IDateTimeProvider"/> or <see cref="DateTimeProvider"/> is used.</summary>
    public const string ObsoleteDiagnosticId = "TAS0001";

    internal const string ObsoleteMessage =
        "Use System.TimeProvider (TimeProvider.System in production, FakeTimeProvider in tests). IDateTimeProvider will be removed in 3.0.";

    private readonly System.TimeProvider timeProvider;

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
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public System.DateTime Now => System.DateTime.SpecifyKind(timeProvider.GetLocalNow().DateTime, System.DateTimeKind.Local);

    /// <inheritdoc/>
    public System.DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    /// <inheritdoc/>
    public System.TimeSpan TimeOfDay => timeProvider.GetLocalNow().TimeOfDay;

    /// <inheritdoc/>
    public System.TimeSpan UtcTimeOfDay => timeProvider.GetUtcNow().TimeOfDay;

    /// <summary>
    /// Gets the time that has passed since midnight in UTC. Misspelled name kept for source compatibility.
    /// </summary>
    [System.Obsolete("Use UtcTimeOfDay.")]
    public System.TimeSpan UtcTimeOfDaty => UtcTimeOfDay;

    /// <inheritdoc/>
    public System.DateTime Today => Now.Date;

    /// <inheritdoc/>
    public System.DateTime UtcToday => UtcNow.Date;
}
