namespace TailoredApps.Shared.DateTime
{
    public class DateTimeProvider : IDateTimeProvider
    {
        public System.DateTime Now => System.DateTime.Now;

        public System.DateTime UtcNow => System.DateTime.UtcNow;

        public System.TimeSpan TimeOfDay => System.DateTime.Now.TimeOfDay;

        public System.TimeSpan UtcTimeOfDaty => System.DateTime.UtcNow.TimeOfDay;

        public System.DateTime Today => System.DateTime.Today;

        public System.DateTime UtcToday => System.DateTime.UtcNow.Date;
    }
}