using System.Globalization;

namespace TailoredApps.Demo.Client.Services;

public static class Money
{
    /// <summary>An amount as the shop shows it everywhere, whatever the browser's language is.</summary>
    public static string Format(decimal amount, string currency = "PLN")
        => string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} {currency}");
}
