namespace TailoredApps.Demo.WebApi.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinimumKeyLength = 32;

    /// <summary>The symmetric signing key. Never in appsettings.json: a secret on a server, appsettings.Development.json locally.</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "tailoredapps-demo";

    public string Audience { get; set; } = "tailoredapps-demo";
}
