namespace TailoredApps.Shared.DateTime
{
    public interface IDateTimeProvider
    {
        System.DateTime Now { get; }
        System.DateTime UtcNow { get; }
        System.TimeSpan TimeOfDay { get; }
        System.TimeSpan UtcTimeOfDaty { get; }
        System.DateTime Today { get; }
        System.DateTime UtcToday { get; }
    }
}