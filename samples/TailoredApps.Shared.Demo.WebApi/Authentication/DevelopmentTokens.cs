using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TailoredApps.Demo.WebApi.Authentication;

/// <summary>
/// Issues a token for local work, in the Development environment only (Startup maps it nowhere else). In a
/// deployment the tokens come from the auth service that shares the signing key or publishes its public one.
/// </summary>
public static class DevelopmentTokens
{
    public static IEndpointRouteBuilder MapDevelopmentTokenEndpoint(this IEndpointRouteBuilder app)
    {
        // POST /api/dev/token?name=anna&role=CatalogAdmin
        app.MapPost("/api/dev/token", (IOptions<JwtOptions> options, string? name, string? role)
            => Results.Ok(new { token = Create(options.Value, name ?? "developer", role is null ? [] : [role]) }));
        return app;
    }

    public static string Create(JwtOptions options, string name, params string[] roles)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = DateTime.UtcNow.AddHours(1),
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, name), .. roles.Select(role => new Claim(ClaimTypes.Role, role))]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256),
        });
}
