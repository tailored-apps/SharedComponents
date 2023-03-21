namespace TailoredApps.Shared.DateTime
{
    /// <summary>
    /// Simple implementation used for <see cref="IDateTimeProvider"/> 
    /// </summary>
    public class DateTimeProvider : IDateTimeProvider
    {
        /// <summary>
        /// Basic implementation wchich returns current system DateTime based on <see cref="System.DateTime.Now"/>
        /// </summary>
        public System.DateTime Now => System.DateTime.Now;

        /// <summary>
        /// Basic implementation wchich returns current system DateTime converted to Utc based on <see cref="System.DateTime.UtcNow"/>
        /// </summary>
        public System.DateTime UtcNow => System.DateTime.UtcNow;

        /// <summary>
        /// Basic implementation wchich returns current system Time of day based on <see cref="System.DateTime.TimeOfDay"/>
        /// </summary>
        public System.TimeSpan TimeOfDay => System.DateTime.Now.TimeOfDay;

        /// <summary>
        /// Basic implementation wchich returns current system Time of day converted to Utc based on <see cref="System.DateTime.TimeOfDay"/>
        /// </summary>
        public System.TimeSpan UtcTimeOfDaty => System.DateTime.UtcNow.TimeOfDay;

        /// <summary>
        /// Basic implementation wchich returns current system date based on <see cref="System.DateTime.Today"/>
        /// </summary>
        public System.DateTime Today => System.DateTime.Today;

        /// <summary>
        /// Basic implementation wchich returns current system date converted to Utc based on <see cref="System.DateTime.Date"/>
        /// </summary>
        public System.DateTime UtcToday => System.DateTime.UtcNow.Date;
    }
}