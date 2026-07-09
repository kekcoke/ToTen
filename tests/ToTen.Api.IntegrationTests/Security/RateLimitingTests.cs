using System.Net;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Audit finding 2.1: write/money endpoints and the public search endpoint had no rate
/// limiting at all. Verifies the "strict" policy (10 requests/minute per client) rejects
/// requests past the limit with 429.
/// </summary>
public class RateLimitingTests(ToTenWebApplicationFactory factory) : IClassFixture<ToTenWebApplicationFactory>
{
    [Fact]
    public async Task StrictPolicyEndpoint_ExceedingLimit_Returns429()
    {
        var client = factory.CreateUnauthenticatedClient();

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 11; i++)
        {
            using var response = await client.GetAsync("/api/listings/search", TestContext.Current.CancellationToken);
            lastStatus = response.StatusCode;

            if (i < 10)
            {
                Assert.NotEqual(HttpStatusCode.TooManyRequests, lastStatus);
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}
