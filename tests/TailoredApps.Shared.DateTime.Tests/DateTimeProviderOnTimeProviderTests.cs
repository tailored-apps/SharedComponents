// The types under test are obsolete on purpose: they stay until 3.0 for code that has not moved to TimeProvider.
#pragma warning disable TAS0001
#pragma warning disable CS0618

using System;
using Xunit;

namespace TailoredApps.Shared.DateTime.Tests;

public class DateTimeProviderOnTimeProviderTests
{
    /// <summary>A clock stopped at a chosen instant, in a chosen local time zone.</summary>
    private sealed class StoppedClock : TimeProvider
    {
        private readonly DateTimeOffset utcNow;
        private readonly TimeZoneInfo zone;

        public StoppedClock(DateTimeOffset utcNow, TimeSpan offset)
        {
            this.utcNow = utcNow;
            zone = TimeZoneInfo.CreateCustomTimeZone("test", offset, "test", "test");
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => zone;
    }

    // 23:30 UTC on 31 December is already 01:30 on 1 January two hours east of Greenwich.
    private static readonly DateTimeOffset Instant = new(2026, 12, 31, 23, 30, 15, TimeSpan.Zero);

    private static DateTimeProvider CreateSut(double offsetHours = 2) => new(new StoppedClock(Instant, TimeSpan.FromHours(offsetHours)));

    [Fact]
    public void When_Clock_Is_Given_Should_Report_Its_Utc_Time_With_Utc_Kind()
    {
        var sut = CreateSut();

        Assert.Equal(new System.DateTime(2026, 12, 31, 23, 30, 15), sut.UtcNow);
        Assert.Equal(DateTimeKind.Utc, sut.UtcNow.Kind);
    }

    [Fact]
    public void When_Clock_Is_Given_Should_Report_Local_Time_In_The_Clocks_Time_Zone_With_Local_Kind()
    {
        var sut = CreateSut();

        Assert.Equal(new System.DateTime(2027, 1, 1, 1, 30, 15), sut.Now);
        Assert.Equal(DateTimeKind.Local, sut.Now.Kind);
    }

    [Fact]
    public void When_Local_Date_Differs_From_Utc_Date_Should_Report_Each_In_Its_Own_Zone()
    {
        var sut = CreateSut();

        Assert.Equal(new System.DateTime(2027, 1, 1), sut.Today);
        Assert.Equal(DateTimeKind.Local, sut.Today.Kind);
        Assert.Equal(new System.DateTime(2026, 12, 31), sut.UtcToday);
        Assert.Equal(DateTimeKind.Utc, sut.UtcToday.Kind);
    }

    [Fact]
    public void When_Time_Of_Day_Is_Read_Should_Report_It_In_Local_And_Utc_Zones()
    {
        var sut = CreateSut();

        Assert.Equal(new TimeSpan(1, 30, 15), sut.TimeOfDay);
        Assert.Equal(new TimeSpan(23, 30, 15), sut.UtcTimeOfDay);
    }

    [Fact]
    public void When_Misspelled_Member_Is_Used_Should_Return_The_Same_Value_As_UtcTimeOfDay()
    {
        var sut = CreateSut();
        IDateTimeProvider throughInterface = sut;

        Assert.Equal(sut.UtcTimeOfDay, sut.UtcTimeOfDaty);
        Assert.Equal(sut.UtcTimeOfDay, throughInterface.UtcTimeOfDaty);
    }

    [Fact]
    public void When_Clock_Is_West_Of_Greenwich_Should_Report_The_Earlier_Local_Time()
    {
        var sut = CreateSut(offsetHours: -5);

        Assert.Equal(new System.DateTime(2026, 12, 31, 18, 30, 15), sut.Now);
        Assert.Equal(new System.DateTime(2026, 12, 31), sut.Today);
    }

    [Fact]
    public void When_No_Clock_Is_Given_Should_Read_The_System_Clock()
    {
        var before = TimeProvider.System.GetUtcNow().UtcDateTime;
        var now = new DateTimeProvider().UtcNow;
        var after = TimeProvider.System.GetUtcNow().UtcDateTime;

        Assert.InRange(now, before, after);
    }

    [Fact]
    public void When_Clock_Is_Null_Should_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DateTimeProvider(null));
    }

    [Fact]
    public void When_Types_Are_Inspected_Should_Be_Marked_Obsolete_With_A_Diagnostic_Id_Pointing_To_TimeProvider()
    {
        foreach (var type in new[] { typeof(IDateTimeProvider), typeof(DateTimeProvider) })
        {
            var attribute = Assert.Single(type.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false));
            var obsolete = Assert.IsType<ObsoleteAttribute>(attribute);
            Assert.Equal("TAS0001", obsolete.DiagnosticId);
            Assert.Contains("TimeProvider", obsolete.Message, StringComparison.Ordinal);
            Assert.False(obsolete.IsError);
        }
    }
}
