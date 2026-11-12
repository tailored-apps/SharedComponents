namespace TailoredApps.Shared.DateTime;

/// <summary>
/// A clock that can be replaced in tests. Superseded by <see cref="System.TimeProvider"/>, which is part of
/// .NET: inject <see cref="System.TimeProvider"/> in new code and use this interface only for code that
/// has not been moved yet. <see cref="DateTimeProvider"/> reads the same <see cref="System.TimeProvider"/>,
/// so both can share one clock while a code base migrates.
/// </summary>
[System.Obsolete(DateTimeProvider.ObsoleteMessage, DiagnosticId = DateTimeProvider.ObsoleteDiagnosticId)]
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in the local time zone of the clock (<see cref="System.DateTimeKind.Local"/>).
    /// </summary>
    System.DateTime Now { get; }

    /// <summary>
    /// Gets the current date and time in UTC (<see cref="System.DateTimeKind.Utc"/>).
    /// </summary>
    System.DateTime UtcNow { get; }

    /// <summary>
    /// Gets the time that has passed since midnight in the local time zone of the clock.
    /// </summary>
    System.TimeSpan TimeOfDay { get; }

    /// <summary>
    /// Gets the time that has passed since midnight in UTC.
    /// </summary>
    System.TimeSpan UtcTimeOfDay { get; }

    /// <summary>
    /// Gets the time that has passed since midnight in UTC. Misspelled name kept for source compatibility.
    /// </summary>
    [System.Obsolete("Use UtcTimeOfDay.")]
    System.TimeSpan UtcTimeOfDaty => UtcTimeOfDay;

    /// <summary>
    /// Gets the current date in the local time zone of the clock, with the time set to midnight.
    /// </summary>
    System.DateTime Today { get; }

    /// <summary>
    /// Gets the current date in UTC, with the time set to midnight.
    /// </summary>
    System.DateTime UtcToday { get; }
}
