using System.Net.Http.Json;

namespace TailoredApps.Demo.Client.Services;

/// <summary>
/// Who is using the client. A deployed client would get its token from the organisation's auth service
/// (OIDC); this one asks the service's Development-only token endpoint, which stands in for it. The token is
/// kept in memory: reloading the page signs out.
/// </summary>
public sealed class Session(HttpClient http)
{
    public const string CatalogAdmin = "CatalogAdmin";

    public event Action? Changed;

    public string? Token { get; private set; }

    public bool IsCatalogAdmin => Token is not null;

    public async Task SignInAsCatalogAdminAsync()
    {
        using var response = await http.PostAsync($"api/dev/token?name=catalogue-admin&role={CatalogAdmin}", content: null);
        if (!response.IsSuccessStatusCode)
            throw await ApiException.FromAsync(response, CancellationToken.None);

        Token = (await response.Content.ReadFromJsonAsync<TokenAnswer>())?.Token;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        Token = null;
        Changed?.Invoke();
    }

    private sealed class TokenAnswer
    {
        public string? Token { get; set; }
    }
}
