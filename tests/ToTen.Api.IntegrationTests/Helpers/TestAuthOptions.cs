using Microsoft.AspNetCore.Authentication;

namespace ToTen.Api.IntegrationTests.Helpers;

public class TestAuthOptions : AuthenticationSchemeOptions
{
    public bool AuthenticationSucceeds { get; set; } = true;
    public string? Email { get; set; }
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string[] Roles { get; set; } = [];
    public Guid? OrganizationId { get; set; }
}

