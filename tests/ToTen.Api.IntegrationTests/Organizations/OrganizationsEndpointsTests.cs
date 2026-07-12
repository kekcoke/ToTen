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

    [Fact]
    public async Task GetMyOrganizations_ReturnsOnlyCallerOrgs_Paginated()
    {
        var client = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        for (var i = 0; i < 3; i++)
        {
            var createResponse = await client.PostAsJsonAsync(
                "/api/organizations",
                new CreateOrganizationRequest($"My Org {i}", "Household"),
                TestContext.Current.CancellationToken);
            createResponse.EnsureSuccessStatusCode();
        }

        // Noise: a different caller's org should not leak in.
        var otherClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());
        var otherOrgResponse = await otherClient.PostAsJsonAsync(
            "/api/organizations",
            new CreateOrganizationRequest("Other Org", "Household"),
            TestContext.Current.CancellationToken);
        otherOrgResponse.EnsureSuccessStatusCode();

        var page1 = await client.GetAsync("/api/organizations?page=1&pageSize=2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        Assert.Equal("3", page1.Headers.GetValues("X-Total-Count").Single());
        var page1Orgs = await page1.Content.ReadFromJsonAsync<List<OrganizationResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page1Orgs);
        Assert.Equal(2, page1Orgs.Count);
        Assert.All(page1Orgs, o => Assert.StartsWith("My Org", o.Name));

        var page2 = await client.GetAsync("/api/organizations?page=2&pageSize=2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page2.StatusCode);
        var page2Orgs = await page2.Content.ReadFromJsonAsync<List<OrganizationResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page2Orgs);
        Assert.Single(page2Orgs);
        Assert.All(page2Orgs, o => Assert.StartsWith("My Org", o.Name));
    }

    [Fact]
    public async Task GetMyOrganizations_ExcludesSoftDeleted()
    {
        var client = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new CreateOrganizationRequest("Soon Deleted Org", "Household"),
            TestContext.Current.CancellationToken);
        var org = await createResponse.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(org);

        var deleteResponse = await client.DeleteAsync($"/api/organizations/{org.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var response = await client.GetAsync("/api/organizations", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orgs = await response.Content.ReadFromJsonAsync<List<OrganizationResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(orgs);
        Assert.DoesNotContain(orgs, o => o.Id == org.Id);
    }

    [Fact]
    public async Task GetMyOrganizations_NoAuth_Returns401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync("/api/organizations", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RenameOrganization_ByOwner_ReturnsOk_AndNameUpdated()
    {
        var createRequest = new CreateOrganizationRequest("Old Name Org", "Household");
        var createResponse = await _client.PostAsJsonAsync("/api/organizations", createRequest, TestContext.Current.CancellationToken);
        var org = await createResponse.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(org);

        var renameRequest = new RenameOrganizationRequest("New Name Org");
        var response = await _client.PatchAsJsonAsync($"/api/organizations/{org.Id}", renameRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("New Name Org", result.Name);

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var updated = await ctx.Organizations.FindAsync([org.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("New Name Org", updated.Name);
    }

    [Fact]
    public async Task RenameOrganization_NotFoundOrDeleted_Returns404()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/organizations/{Guid.NewGuid()}",
            new RenameOrganizationRequest("Whatever"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RenameOrganization_EmptyName_ReturnsBadRequest()
    {
        var createRequest = new CreateOrganizationRequest("Empty Name Target Org", "Household");
        var createResponse = await _client.PostAsJsonAsync("/api/organizations", createRequest, TestContext.Current.CancellationToken);
        var org = await createResponse.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(org);

        var response = await _client.PatchAsJsonAsync(
            $"/api/organizations/{org.Id}",
            new RenameOrganizationRequest(""),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
