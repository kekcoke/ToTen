using Microsoft.AspNetCore.Authentication;

namespace ToTen.Api.IntegrationTests.Helpers;

public class TestAuthOptions : AuthenticationSchemeOptions
{
    public bool AuthenticationSucceeds { get; set; } = true;
    public string? Email { get; set; }
}

