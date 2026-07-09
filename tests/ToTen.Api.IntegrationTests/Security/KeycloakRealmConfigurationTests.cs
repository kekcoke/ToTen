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
