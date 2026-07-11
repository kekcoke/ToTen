namespace ToTen.Api.Shared.Identity;

/// <summary>
/// Wraps the server-side Authorization Code token exchange the web BFF performs against
/// Keycloak's token endpoint. Kept as a thin, substitutable seam (rather than using the
/// built-in OpenIdConnectHandler, whose internal backchannel HttpClient isn't cleanly
/// fakeable via IHttpClientFactory from a test host) so integration tests can replace it
/// with an NSubstitute mock the same way IBus/IQRCodeService are substituted today.
/// </summary>
public interface IKeycloakTokenClient
{
    Task<KeycloakTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken);

    Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task EndSessionAsync(string idToken, CancellationToken cancellationToken);
}

public record KeycloakTokenResponse(string AccessToken, string? RefreshToken, string IdToken, int ExpiresIn);
