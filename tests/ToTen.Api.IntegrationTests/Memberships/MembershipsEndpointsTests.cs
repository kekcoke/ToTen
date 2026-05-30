using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Memberships;
using ToTen.Api.Features.Organizations;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Memberships;

public class MembershipsEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateOrgAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/organizations",
            new CreateOrganizationRequest("Membership Test Org", "Household"),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var org = await response.Content.ReadFromJsonAsync<OrganizationResponse>(TestContext.Current.CancellationToken);
        return org!.Id;
    }

    [Fact]
    public async Task InviteMember_ByOwner_ReturnsOk_AndMembershipExists()
    {
        var orgId = await CreateOrgAsync();
        var invitedUserId = Guid.NewGuid();
        var request = new InviteMemberRequest(invitedUserId, "member");

        var response = await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MembershipResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(orgId, result.OrganizationId);
        Assert.Equal(invitedUserId, result.UserId);
        Assert.Equal("member", result.Role);

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var membership = ctx.OrganizationMemberships
            .FirstOrDefault(m => m.OrganizationId == orgId && m.UserId == invitedUserId.ToString());
        Assert.NotNull(membership);
    }

    [Fact]
    public async Task RemoveMember_ByOwner_ReturnsNoContent()
    {
        var orgId = await CreateOrgAsync();
        var invitedUserId = Guid.NewGuid();

        await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(invitedUserId, "member"),
            TestContext.Current.CancellationToken);

        var response = await _client.DeleteAsync(
            $"/api/organizations/{orgId}/members/{invitedUserId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task InviteMember_ByNonOwner_ReturnsForbidden()
    {
        var orgId = await CreateOrgAsync();
        var nonOwnerClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await nonOwnerClient.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(Guid.NewGuid(), "member"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
