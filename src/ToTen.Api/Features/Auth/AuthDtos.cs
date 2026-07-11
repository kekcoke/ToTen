namespace ToTen.Api.Features.Auth;

public record MeResponse(string Sub, string? Email, string[] Roles);

public record CsrfTokenResponse(string Token);

/// <summary>
/// PKCE state stashed in the short-lived transient cookie between /auth/login and
/// /auth/callback. Encrypted via IDataProtector — never sent to the browser in plaintext.
/// </summary>
internal record OidcTransactionState(string State, string CodeVerifier, string ReturnUrl);
