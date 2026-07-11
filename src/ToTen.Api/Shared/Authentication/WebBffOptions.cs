using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Shared.Authentication;

/// <summary>
/// Configuration for the web Backend-For-Frontend auth broker (Features/Auth). Separate from
/// <see cref="AuthOptions"/>, which remains the bearer-validation config (Authority/Audience/ApiScope)
/// used by both mobile tokens and the BFF's own outbound calls to Keycloak.
/// </summary>
public class WebBffOptions
{
    public const string SectionName = "Auth:WebBff";

    /// <summary>
    /// The ToTen-web-bff confidential client's client ID in Keycloak.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The ToTen-web-bff confidential client's secret. Sourced from Key Vault in deployed
    /// environments (see terraform/modules/apps), or the matching Aspire parameter locally.
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The callback URL registered as this client's redirectUris entry in the realm.
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}
