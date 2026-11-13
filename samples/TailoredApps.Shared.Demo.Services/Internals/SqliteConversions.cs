using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TailoredApps.Demo.Services.Internals;

/// <summary>
/// SQLite has no decimal or date-with-offset type and cannot sort or compare them as text. Amounts are stored
/// in minor units (grosze) - exact, unlike a floating-point column - and dates as numbers, so that sorting by
/// price works. A database with a decimal type needs none of this.
/// </summary>
public static class SqliteConversions
{
    /// <summary>An amount with two decimal places, stored as a whole number of hundredths.</summary>
    public static readonly ValueConverter<decimal, long> MinorUnits = new(
        amount => (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero),
        minorUnits => minorUnits / 100m);

    /// <summary>
    /// A point in time, stored as UTC ticks: exact, unlike DateTimeOffsetToBinaryConverter, which drops
    /// precision. The offset is not kept; the service records every time in UTC.
    /// </summary>
    public static readonly ValueConverter<DateTimeOffset, long> UtcTicks = new(
        time => time.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
}
