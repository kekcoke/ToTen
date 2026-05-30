using System.Net;
using System.Net.Http.Json;
using ToTen.Api.Features.Memberships;
using ToTen.Api.Features.Organizations;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Multi-user organization access tests.
/// Validates that ownership and org membership correctly gate resource access.
/// </summary>
public class OrganizationAccessTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateOrgAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new CreateOrganizationRequest("Access Test Org", "Business"),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var org = await response.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        return org!.Id;
    }

    [Fact]
    public async Task OrgOwner_CanInviteMembers_ReturnsOk()
    {
        var orgId = await CreateOrgAsync(_client);

        var response = await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(Guid.NewGuid(), "member"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonMember_CannotInviteToOrg_ReturnsForbidden()
    {
        var orgId = await CreateOrgAsync(_client);
        var nonMemberClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await nonMemberClient.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(Guid.NewGuid(), "member"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrossTenant_OwnerOfOrgA_CannotInviteToOrgB_ReturnsForbidden()
    {
        var ownerBId = Guid.NewGuid();
        var ownerBClient = factory.CreateAuthenticatedClient(userId: ownerBId);

        // Owner B creates their own org
        var orgBId = await CreateOrgAsync(ownerBClient);

        // Default user (Owner A) has no membership in org B
        var response = await _client.PostAsJsonAsync(
            $"/api/organizations/{orgBId}/members",
            new InviteMemberRequest(Guid.NewGuid(), "member"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
