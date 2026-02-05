using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToTen.Api.IntegrationTests.Helpers;

public class TestAuthHandler(
    IOptionsMonitor<TestAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<TestAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Options.AuthenticationSucceeds)
        {
            return Task.FromResult(AuthenticateResult.Fail("Authentication failed"));
        }

        List<Claim> claims = [];

        if (!string.IsNullOrWhiteSpace(Options.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, Options.Email));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}