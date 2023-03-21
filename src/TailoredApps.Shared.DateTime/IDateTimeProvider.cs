namespace TailoredApps.Shared.DateTime
{
    /// <summary>
    /// Simple interface for mocking <see cref="System.DateTime"/> used for unit testing and configuration in mocks time used in tests.
    /// </summary>
    public interface IDateTimeProvider
    {
        /// <summary>
        /// Gets current system DateTime
        /// </summary>
        System.DateTime Now { get; }
        /// <summary>
        /// Gets current system DateTime converted to Utc format.
        /// </summary>
        System.DateTime UtcNow { get; }
        /// <summary>
        /// Gets Time of Day and returns Timespan
        /// </summary>
        System.TimeSpan TimeOfDay { get; }
        /// <summary>
        /// Gets Time of Day and returns Timespan converted to Utc format
        /// </summary>
        System.TimeSpan UtcTimeOfDaty { get; }
        /// <summary>
        /// Current system Date
        /// </summary>
        System.DateTime Today { get; }
        /// <summary>
        /// Current system Date converted to Utc.
        /// </summary>
        System.DateTime UtcToday { get; }
    }
}