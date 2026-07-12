using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Organizations;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Organizations;

public class OrganizationsEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateOrganization_ReturnsCreated_AndOwnerMembershipExists()
    {
        var request = new CreateOrganizationRequest("Test Household", "Household");

        var response = await _client.PostAsJsonAsync("/api/organizations", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var org = await response.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(org);
        Assert.Equal("Test Household", org.Name);

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var membership = ctx.OrganizationMemberships
            .FirstOrDefault(m => m.OrganizationId == org.Id);
        Assert.NotNull(membership);
        Assert.Equal("Owner", membership.Role);
        Assert.Equal(factory.DefaultTestUserId.ToString(), membership.UserId);
    }

    [Fact]
    public async Task CreateOrganization_InvalidRequest_ReturnsBadRequest()
    {
        var request = new CreateOrganizationRequest("Bad Org", "NotARealType");

        var response = await _client.PostAsJsonAsync("/api/organizations", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganization_WithAuth_ReturnsOk()
    {
        var createRequest = new CreateOrganizationRequest("Get Test Org", "Business");
        var createResponse = await _client.PostAsJsonAsync("/api/organizations", createRequest, TestContext.Current.CancellationToken);
        var org = await createResponse.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(org);

        var response = await _client.GetAsync($"/api/organizations/{org.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Get Test Org", result.Name);
    }

    [Fact]
    public async Task GetOrganization_NoAuth_Returns401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync($"/api/organizations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrganization_ByOwner_ReturnsNoContent_AndOrgSoftDeleted()
    {
        var createRequest = new CreateOrganizationRequest("To Delete Org", "Household");
        var createResponse = await _client.PostAsJsonAsync("/api/organizations", createRequest, TestContext.Current.CancellationToken);
        var org = await createResponse.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(org);

        var response = await _client.DeleteAsync($"/api/organizations/{org.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var deleted = await ctx.Organizations.FindAsync([org.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(deleted);
        Assert.NotNull(deleted.DateDeleted);
    }

    [Fact]
    public async Task GetOrganization_AfterDelete_ReturnsNotFound()
    {
        var createRequest = new CreateOrganizationRequest("To Delete And Get Org", "Household");
        var createResponse = await _client.PostAsJsonAsync("/api/organizations", createRequest, TestContext.Current.CancellationToken);
        var org = await createResponse.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(org);

        await _client.DeleteAsync($"/api/organizations/{org.Id}", TestContext.Current.CancellationToken);

        var response = await _client.GetAsync($"/api/organizations/{org.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
