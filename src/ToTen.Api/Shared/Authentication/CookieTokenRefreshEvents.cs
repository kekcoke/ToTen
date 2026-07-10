using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Shared.Authentication;

/// <summary>
/// Keeps the web BFF's stored access token fresh across the cookie's 30-minute session, given
/// the realm's 5-minute access token lifespan. Runs on every cookie-authenticated request via
/// CookieAuthenticationOptions.Events.OnValidatePrincipal.
/// </summary>
public static class CookieTokenRefreshEvents
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    public static async Task RefreshIfNeededAsync(CookieValidatePrincipalContext context)
    {
        var properties = context.Properties;
        var expiresAtRaw = properties.GetTokenValue("expires_at");
        var refreshToken = properties.GetTokenValue("refresh_token");

        if (expiresAtRaw is null || refreshToken is null)
        {
            return;
        }

        if (!DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresAt))
        {
            return;
        }

        if (DateTimeOffset.UtcNow < expiresAt - RefreshMargin)
        {
            return;
        }

        var tokenClient = context.HttpContext.RequestServices.GetRequiredService<IKeycloakTokenClient>();

        try
        {
            var refreshed = await tokenClient.RefreshAsync(refreshToken, context.HttpContext.RequestAborted);

            properties.UpdateTokenValue("access_token", refreshed.AccessToken);
            properties.UpdateTokenValue("id_token", refreshed.IdToken);
            if (refreshed.RefreshToken is not null)
            {
                properties.UpdateTokenValue("refresh_token", refreshed.RefreshToken);
            }
            properties.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn).ToString("o", CultureInfo.InvariantCulture));

            context.ShouldRenew = true;
        }
        catch (HttpRequestException)
        {
            // Realm has revokeRefreshToken:true + refreshTokenMaxReuse:0 — a revoked/reused
            // refresh token means the session can't be extended. Force re-login.
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
