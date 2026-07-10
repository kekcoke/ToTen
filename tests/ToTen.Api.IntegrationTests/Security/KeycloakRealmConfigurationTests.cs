using System.Linq;
using System.Text.Json;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Regression coverage for audit finding 3.2: refresh-token rotation must be enabled
/// (revokeRefreshToken) and offline sessions must have an absolute expiry cap
/// (offlineSessionMaxLifespanEnabled), since mobile devices carry higher token-theft
/// exposure than a browser. Reads the realm export directly rather than a live Keycloak
/// instance, so it guards against the flags silently regressing in a future realm edit.
/// </summary>
public class KeycloakRealmConfigurationTests
{
    [Fact]
    public void Realm_RefreshTokenRotationIsEnabled()
    {
        var realm = LoadRealm();

        Assert.True(realm.GetProperty("revokeRefreshToken").GetBoolean());
    }

    [Fact]
    public void Realm_OfflineSessionAbsoluteExpiryIsEnabled()
    {
        var realm = LoadRealm();

        Assert.True(realm.GetProperty("offlineSessionMaxLifespanEnabled").GetBoolean());
    }

    // Audit finding 1.8: mobile needs a dedicated public client with PKCE and a pinned
    // (non-wildcard) native redirect — see docs/section-2-flagged-issues.md.
    [Fact]
    public void Realm_MobileClientExists_IsPublicWithPkce()
    {
        var client = FindClient("ToTen-mobile");

        Assert.True(client.GetProperty("publicClient").GetBoolean());
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("implicitFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.False(client.GetProperty("serviceAccountsEnabled").GetBoolean());
        Assert.Equal("S256", client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
    }

    [Fact]
    public void Realm_MobileClientRedirectUri_IsPinnedNotWildcard()
    {
        var client = FindClient("ToTen-mobile");
        var redirectUris = client.GetProperty("redirectUris").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();

        Assert.Equal(["com.toten.app://auth/callback"], redirectUris);
        Assert.DoesNotContain(redirectUris, uri => uri.Contains('*'));
    }

    [Fact]
    public void Realm_MobileClientHasAudienceScopeByDefault()
    {
        var client = FindClient("ToTen-mobile");
        var defaultScopes = client.GetProperty("defaultClientScopes").EnumerateArray().Select(e => e.GetString()).ToArray();

        Assert.Contains("ToTen_api.all", defaultScopes);
    }

    [Fact]
    public void Realm_WebBffClientExists_IsConfidentialWithPkce()
    {
        var client = FindClient("ToTen-web-bff");

        Assert.False(client.GetProperty("publicClient").GetBoolean());
        Assert.Equal("client-secret", client.GetProperty("clientAuthenticatorType").GetString());
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("implicitFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.Equal("S256", client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
        Assert.False(string.IsNullOrEmpty(client.GetProperty("secret").GetString()));
    }

    [Fact]
    public void Realm_ApiClientIsBearerOnly()
    {
        var client = FindClient("ToTen-api");

        Assert.True(client.GetProperty("bearerOnly").GetBoolean());
        Assert.False(client.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("implicitFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.False(client.GetProperty("serviceAccountsEnabled").GetBoolean());
    }

    [Fact]
    public void Realm_SwaggerClientEnforcesPkce()
    {
        var client = FindClient("ToTen-api-swagger");

        Assert.Equal("S256", client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
    }

    private static JsonElement FindClient(string clientId)
    {
        var realm = LoadRealm();
        foreach (var client in realm.GetProperty("clients").EnumerateArray())
        {
            if (client.GetProperty("clientId").GetString() == clientId)
            {
                return client;
            }
        }

        throw new InvalidOperationException($"Client '{clientId}' not found in realm export.");
    }

    private static JsonElement LoadRealm()
    {
        var realmPath = Path.Join(FindRepoRoot(), "src", "ToTen.AppHost", "realms", "ToTen-realm.json");
        using var stream = File.OpenRead(realmPath);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.GetFiles("*.slnx").Any() && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root (no .slnx/.sln found above " + AppContext.BaseDirectory + ")");
    }
}
