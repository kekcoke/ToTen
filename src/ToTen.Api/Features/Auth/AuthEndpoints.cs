using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ToTen.Api.Shared.Authentication;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;

namespace ToTen.Api.Features.Auth;

/// <summary>
/// Server-side Backend-For-Frontend auth broker for the web client. Performs the Authorization
/// Code + PKCE exchange against Keycloak's ToTen-web-bff confidential client on the browser's
/// behalf, and hands the browser only an encrypted, HttpOnly session cookie — raw tokens never
/// reach page JavaScript. Mobile clients never call anything in this file; they talk to Keycloak
/// directly (see docs/section-2-flagged-issues.md 1.8 and the plan this slice implements).
/// </summary>
public static class AuthEndpoints
{
    private const string TransientCookieName = "ToTen-OidcTxn";
    private const string OidcTransactionProtectorPurpose = "ToTen.Api.Auth.OidcTransaction";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);

        group.MapGet("/login", HandleLogin)
            .WithName("AuthLogin")
            .AllowAnonymous();

        group.MapGet("/callback", HandleCallbackAsync)
            .WithName("AuthCallback")
            .AllowAnonymous();

        group.MapPost("/logout", HandleLogoutAsync)
            .WithName("AuthLogout")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme });

        group.MapGet("/me", HandleMe)
            .WithName("AuthMe")
            .RequireAuthorization();

        group.MapGet("/csrf", HandleCsrf)
            .WithName("AuthCsrf")
            .AllowAnonymous();
    }

    private static IResult HandleLogin(
        HttpContext httpContext,
        IOptions<AuthOptions> authOptions,
        IOptions<WebBffOptions> webBffOptions,
        IDataProtectionProvider dataProtectionProvider,
        string? returnUrl)
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = ComputeCodeChallenge(codeVerifier);
        var state = GenerateRandomToken();

        var transaction = new OidcTransactionState(state, codeVerifier, returnUrl ?? "/");
        var protector = dataProtectionProvider.CreateProtector(OidcTransactionProtectorPurpose);
        var protectedTransaction = protector.Protect(JsonSerializer.Serialize(transaction));

        httpContext.Response.Cookies.Append(TransientCookieName, protectedTransaction, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(5),
            Path = "/auth",
        });

        var authorizeUrl = QueryHelpers.AddQueryString($"{authOptions.Value.Authority}/protocol/openid-connect/auth", new Dictionary<string, string?>
        {
            ["client_id"] = webBffOptions.Value.ClientId,
            ["redirect_uri"] = webBffOptions.Value.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid ToTen_api.all",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        });

        return Results.Redirect(authorizeUrl);
    }

    private static async Task<IResult> HandleCallbackAsync(
        HttpContext httpContext,
        string? code,
        string? state,
        IKeycloakTokenClient tokenClient,
        IOptions<WebBffOptions> webBffOptions,
        IDataProtectionProvider dataProtectionProvider)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Results.BadRequest("Missing code or state.");
        }

        if (!httpContext.Request.Cookies.TryGetValue(TransientCookieName, out var protectedTransaction))
        {
            return Results.BadRequest("Missing or expired OIDC transaction state.");
        }

        httpContext.Response.Cookies.Delete(TransientCookieName, new CookieOptions { Path = "/auth" });

        OidcTransactionState transaction;
        try
        {
            var protector = dataProtectionProvider.CreateProtector(OidcTransactionProtectorPurpose);
            transaction = JsonSerializer.Deserialize<OidcTransactionState>(protector.Unprotect(protectedTransaction))
                ?? throw new InvalidOperationException("Empty OIDC transaction payload.");
        }
        catch (CryptographicException)
        {
            return Results.BadRequest("Invalid OIDC transaction state.");
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(transaction.State), Encoding.UTF8.GetBytes(state)))
        {
            return Results.BadRequest("State mismatch.");
        }

        var tokenResponse = await tokenClient.ExchangeCodeAsync(code, transaction.CodeVerifier, webBffOptions.Value.RedirectUri, httpContext.RequestAborted);

        // Raw claims (unmapped "sub"/"realm_access"/"resource_access") deliberately preserved —
        // KeycloakClaimsTransformation expects the same shape it already parses from bearer JWTs.
        var idTokenClaims = new JwtSecurityTokenHandler().ReadJwtToken(tokenResponse.IdToken).Claims;
        var identity = new ClaimsIdentity(idTokenClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties();
        authProperties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = tokenResponse.AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = tokenResponse.RefreshToken ?? string.Empty },
            new AuthenticationToken { Name = "id_token", Value = tokenResponse.IdToken },
            new AuthenticationToken
            {
                Name = "expires_at",
                Value = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            },
        ]);

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        return Results.Redirect(transaction.ReturnUrl);
    }

    private static async Task<IResult> HandleLogoutAsync(HttpContext httpContext, IKeycloakTokenClient tokenClient)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var idToken = authenticateResult.Properties?.GetTokenValue("id_token");

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrEmpty(idToken))
        {
            await tokenClient.EndSessionAsync(idToken, httpContext.RequestAborted);
        }

        return Results.NoContent();
    }

    private static IResult HandleMe(ClaimsPrincipal principal, IIdentityManager identityManager)
    {
        var user = identityManager.GetCurrentUser(principal);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new MeResponse(user.Id.ToString(), user.Email, user.Roles));
    }

    private static IResult HandleCsrf(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }

    private static string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string GenerateRandomToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string ComputeCodeChallenge(string codeVerifier) => Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
