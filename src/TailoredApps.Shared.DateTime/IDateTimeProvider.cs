namespace TailoredApps.Shared.DateTime;

/// <summary>
/// The current date and time as ready-to-use values, read from a clock that can be replaced in tests.
/// </summary>
/// <remarks>
/// <para>
/// This is a convenience view over <see cref="System.TimeProvider"/>, the clock abstraction of .NET:
/// <see cref="DateTimeProvider"/> reads every value from one <see cref="System.TimeProvider"/>, so code that
/// injects this interface and code that injects <see cref="System.TimeProvider"/> directly share one clock.
/// </para>
/// <para>
/// Prefer the <see cref="System.DateTimeOffset"/> members (<see cref="OffsetUtcNow"/>, <see cref="OffsetNow"/>)
/// for values that are stored or sent: they keep the offset from UTC, which a <see cref="System.DateTime"/>
/// only hints at through its <see cref="System.DateTime.Kind"/>.
/// </para>
/// </remarks>
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
    /// Gets the current date and time in the local time zone of the clock, together with that zone's
    /// offset from UTC.
    /// </summary>
    /// <remarks>
    /// The default implementation derives the value from <see cref="Now"/> and the time zone of the machine;
    /// <see cref="DateTimeProvider"/> reports the time zone of its <see cref="System.TimeProvider"/>.
    /// </remarks>
    System.DateTimeOffset OffsetNow => new(System.DateTime.SpecifyKind(Now, System.DateTimeKind.Local));

    /// <summary>
    /// Gets the current date and time in UTC as a <see cref="System.DateTimeOffset"/> with a zero offset.
    /// </summary>
    /// <remarks>The default implementation derives the value from <see cref="UtcNow"/>.</remarks>
    System.DateTimeOffset OffsetUtcNow => new(System.DateTime.SpecifyKind(UtcNow, System.DateTimeKind.Utc));

    /// <summary>
    /// Gets the time that has passed since midnight in the local time zone of the clock.
    /// </summary>
    System.TimeSpan TimeOfDay { get; }

    /// <summary>
    /// Gets the time that has passed since midnight in UTC.
    /// </summary>
    /// <remarks>The default implementation derives the value from <see cref="UtcNow"/>.</remarks>
    System.TimeSpan UtcTimeOfDay => UtcNow.TimeOfDay;

    /// <summary>
    /// Gets the current date in the local time zone of the clock, with the time set to midnight.
    /// </summary>
    System.DateTime Today { get; }

    /// <summary>
    /// Gets the current date in UTC, with the time set to midnight.
    /// </summary>
    System.DateTime UtcToday { get; }
}
