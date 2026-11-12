using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TailoredApps.Shared.DateTime.Tests;

/// <summary>A clock stopped at a chosen instant, in a chosen local time zone.</summary>
internal sealed class StoppedClock : TimeProvider
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

public class DateTimeProviderOnTimeProviderTests
{
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
    public void When_Utc_Offset_Time_Is_Read_Should_Report_The_Instant_With_A_Zero_Offset()
    {
        var sut = CreateSut();

        Assert.Equal(Instant, sut.OffsetUtcNow);
        Assert.Equal(TimeSpan.Zero, sut.OffsetUtcNow.Offset);
    }

    [Fact]
    public void When_Local_Offset_Time_Is_Read_Should_Report_The_Same_Instant_With_The_Clocks_Offset()
    {
        var sut = CreateSut();

        Assert.Equal(Instant, sut.OffsetNow);                                          // the same moment in time
        Assert.Equal(TimeSpan.FromHours(2), sut.OffsetNow.Offset);
        Assert.Equal(new System.DateTime(2027, 1, 1, 1, 30, 15), sut.OffsetNow.DateTime); // shown on the local wall clock
    }

    [Fact]
    public void When_Read_Through_The_Interface_Should_Report_The_Clocks_Time_Zone_Not_The_Machines()
    {
        IDateTimeProvider sut = CreateSut(offsetHours: 9.5);

        Assert.Equal(TimeSpan.FromHours(9.5), sut.OffsetNow.Offset);
        Assert.Equal(Instant, sut.OffsetUtcNow);
        Assert.Equal(new TimeSpan(23, 30, 15), sut.UtcTimeOfDay);
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
    public void When_Clock_Is_West_Of_Greenwich_Should_Report_The_Earlier_Local_Time()
    {
        var sut = CreateSut(offsetHours: -5);

        Assert.Equal(new System.DateTime(2026, 12, 31, 18, 30, 15), sut.Now);
        Assert.Equal(new System.DateTime(2026, 12, 31), sut.Today);
        Assert.Equal(TimeSpan.FromHours(-5), sut.OffsetNow.Offset);
    }

    [Fact]
    public void When_Clock_Is_Given_Should_Expose_It_For_Timers_And_Delays()
    {
        var clock = new StoppedClock(Instant, TimeSpan.Zero);

        Assert.Same(clock, new DateTimeProvider(clock).TimeProvider);
        Assert.Same(TimeProvider.System, new DateTimeProvider().TimeProvider);
    }

    [Fact]
    public void When_No_Clock_Is_Given_Should_Read_The_System_Clock()
    {
        var before = TimeProvider.System.GetUtcNow();
        var now = new DateTimeProvider().OffsetUtcNow;
        var after = TimeProvider.System.GetUtcNow();

        Assert.InRange(now, before, after);
    }

    [Fact]
    public void When_Clock_Is_Null_Should_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DateTimeProvider(null));
    }

    [Fact]
    public void When_Types_Are_Inspected_Should_Have_No_Obsolete_Members()
    {
        // 3.0 removed the misspelled UtcTimeOfDaty; nothing obsolete is left to remove.
        foreach (var type in new[] { typeof(IDateTimeProvider), typeof(DateTimeProvider) })
        {
            Assert.Empty(type.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false));
            Assert.Empty(type.GetMembers().Where(m => m.IsDefined(typeof(ObsoleteAttribute), inherit: false)).Select(m => m.Name));
            Assert.Null(type.GetProperty("UtcTimeOfDaty"));
        }
    }
}

/// <summary>
/// An implementation written against the 1.x interface keeps compiling: the members added in 2.0 have
/// default implementations derived from the ones it already provides.
/// </summary>
public class DateTimeProviderDefaultMembersTests
{
    private sealed class OneDotXClock : IDateTimeProvider
    {
        private static readonly System.DateTime Utc = new(2026, 6, 1, 12, 30, 0, DateTimeKind.Utc);

        public System.DateTime Now => Utc.ToLocalTime();

        public System.DateTime UtcNow => Utc;

        public TimeSpan TimeOfDay => Now.TimeOfDay;

        // The only spelling the 1.x interface had. 3.0 removed it from the interface; an implementation that still
        // declares it keeps compiling, and the property is simply its own.
        public TimeSpan UtcTimeOfDaty => new(1, 2, 3);

        public System.DateTime Today => Now.Date;

        public System.DateTime UtcToday => Utc.Date;
    }

    private static readonly IDateTimeProvider Sut = new OneDotXClock();

    [Fact]
    public void When_Implementation_Predates_The_Offset_Members_Should_Derive_Utc_Offset_Time_From_UtcNow()
    {
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero), Sut.OffsetUtcNow);
        Assert.Equal(TimeSpan.Zero, Sut.OffsetUtcNow.Offset);
    }

    [Fact]
    public void When_Implementation_Predates_The_Offset_Members_Should_Derive_Local_Offset_Time_From_Now()
    {
        // the same instant, expressed with the offset of the machine's time zone
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero), Sut.OffsetNow);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(Sut.Now), Sut.OffsetNow.Offset);
    }

    [Fact]
    public void When_Implementation_Predates_UtcTimeOfDay_Should_Derive_It_From_UtcNow()
    {
        Assert.Equal(new TimeSpan(12, 30, 0), Sut.UtcTimeOfDay);
    }
}

public class DateTimeProviderRegistrationTests
{
    private static readonly DateTimeOffset Instant = new(2026, 12, 31, 23, 30, 15, TimeSpan.Zero);

    [Fact]
    public void When_Registered_Should_Resolve_One_Provider_That_Reads_The_System_Clock()
    {
        using var services = new ServiceCollection().AddDateTimeProvider().BuildServiceProvider();

        var provider = services.GetRequiredService<IDateTimeProvider>();

        Assert.Same(provider, services.GetRequiredService<IDateTimeProvider>());
        Assert.Same(TimeProvider.System, services.GetRequiredService<TimeProvider>());
        Assert.Same(TimeProvider.System, Assert.IsType<DateTimeProvider>(provider).TimeProvider);
    }

    [Fact]
    public void When_Host_Registered_Its_Clock_First_Should_Read_That_Clock()
    {
        var clock = new StoppedClock(Instant, TimeSpan.FromHours(1));
        using var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(clock)
            .AddDateTimeProvider()
            .BuildServiceProvider();

        Assert.Same(clock, services.GetRequiredService<TimeProvider>());
        Assert.Equal(Instant, services.GetRequiredService<IDateTimeProvider>().OffsetUtcNow);
    }

    [Fact]
    public void When_Host_Registers_Its_Clock_Afterwards_Should_Read_That_Clock_Too()
    {
        // a test host typically replaces the clock after the application has registered its services
        var clock = new StoppedClock(Instant, TimeSpan.FromHours(1));
        using var services = new ServiceCollection()
            .AddDateTimeProvider()
            .AddSingleton<TimeProvider>(clock)
            .BuildServiceProvider();

        var provider = services.GetRequiredService<IDateTimeProvider>();

        Assert.Equal(Instant, provider.OffsetUtcNow);
        Assert.Equal(TimeSpan.FromHours(1), provider.OffsetNow.Offset);
    }

    [Fact]
    public void When_A_Provider_Is_Already_Registered_Should_Keep_It()
    {
        var own = new DateTimeProvider(new StoppedClock(Instant, TimeSpan.Zero));
        using var services = new ServiceCollection()
            .AddSingleton<IDateTimeProvider>(own)
            .AddDateTimeProvider()
            .BuildServiceProvider();

        Assert.Same(own, services.GetRequiredService<IDateTimeProvider>());
    }

    [Fact]
    public void When_Registered_Twice_Should_Not_Add_Duplicates()
    {
        var services = new ServiceCollection().AddDateTimeProvider().AddDateTimeProvider();

        Assert.Single(services, d => d.ServiceType == typeof(IDateTimeProvider));
        Assert.Single(services, d => d.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void When_Services_Are_Null_Should_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DateTimeProviderExtensions.AddDateTimeProvider(null));
    }
}
