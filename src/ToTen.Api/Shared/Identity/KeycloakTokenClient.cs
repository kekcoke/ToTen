using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ToTen.Api.Shared.Authentication;

namespace ToTen.Api.Shared.Identity;

public class KeycloakTokenClient(IHttpClientFactory httpClientFactory, IOptions<AuthOptions> authOptions, IOptions<WebBffOptions> webBffOptions) : IKeycloakTokenClient
{
    public const string HttpClientName = "Keycloak";

    public Task<KeycloakTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = webBffOptions.Value.ClientId,
            ["client_secret"] = webBffOptions.Value.ClientSecret,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
        }, cancellationToken);

    public Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = webBffOptions.Value.ClientId,
            ["client_secret"] = webBffOptions.Value.ClientSecret,
            ["refresh_token"] = refreshToken,
        }, cancellationToken);

    public async Task EndSessionAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var endSessionUrl = QueryHelpers.AddQueryString(
                $"{authOptions.Value.Authority}/protocol/openid-connect/logout",
                "id_token_hint", idToken);
            await client.GetAsync(endSessionUrl, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Best-effort — the caller has already cleared the session cookie regardless.
        }
    }

    private async Task<KeycloakTokenResponse> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{authOptions.Value.Authority}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenEndpointResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak token endpoint returned an empty response.");

        return new KeycloakTokenResponse(payload.AccessToken, payload.RefreshToken, payload.IdToken, payload.ExpiresIn);
    }

    private sealed class TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
