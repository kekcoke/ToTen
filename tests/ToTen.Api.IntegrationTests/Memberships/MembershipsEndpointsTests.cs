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
        // Each call spins up a fresh authenticated host (same effective owner
        // identity as the shared _client, since userId defaults to
        // factory.DefaultTestUserId) so org-creation calls don't pile onto the
        // shared _client's "strict" rate-limit budget (10 req/min on
        // POST /api/organizations) across the growing number of tests in this class.
        var ownerClient = factory.CreateAuthenticatedClient(userId: factory.DefaultTestUserId);
        var response = await ownerClient.PostAsJsonAsync(
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
    public async Task InviteMember_InvalidRole_ReturnsBadRequest()
    {
        var orgId = await CreateOrgAsync();
        var request = new InviteMemberRequest(Guid.NewGuid(), "SuperOwner");

        var response = await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    [Fact]
    public async Task ListMembers_ByMember_ReturnsOk_WithPagination()
    {
        var orgId = await CreateOrgAsync();
        var invitedUserId = Guid.NewGuid();

        await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(invitedUserId, "member"),
            TestContext.Current.CancellationToken);

        var memberClient = factory.CreateAuthenticatedClient(userId: invitedUserId);

        var response = await memberClient.GetAsync(
            $"/api/organizations/{orgId}/members?page=1&pageSize=20",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        Assert.Equal("2", response.Headers.GetValues("X-Total-Count").First());

        var members = await response.Content.ReadFromJsonAsync<List<MemberResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(members);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.UserId == invitedUserId.ToString());
        Assert.Contains(members, m => m.UserId == factory.DefaultTestUserId.ToString());
    }

    [Fact]
    public async Task ListMembers_ByNonMember_ReturnsForbidden()
    {
        var orgId = await CreateOrgAsync();
        var nonMemberClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await nonMemberClient.GetAsync(
            $"/api/organizations/{orgId}/members",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListMembers_NoAuth_Returns401()
    {
        var orgId = await CreateOrgAsync();
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync(
            $"/api/organizations/{orgId}/members",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListMembers_OrgNotFoundOrDeleted_Returns404()
    {
        var orgId = await CreateOrgAsync();
        await _client.DeleteAsync($"/api/organizations/{orgId}", TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            $"/api/organizations/{orgId}/members",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_ByOwner_ReturnsOk_AndRoleUpdated()
    {
        var orgId = await CreateOrgAsync();
        var invitedUserId = Guid.NewGuid();

        await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(invitedUserId, "Member"),
            TestContext.Current.CancellationToken);

        var response = await _client.PatchAsJsonAsync(
            $"/api/organizations/{orgId}/members/{invitedUserId}/role",
            new ChangeMemberRoleRequest("Owner"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MembershipResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Owner", result.Role);

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var membership = await ctx.OrganizationMemberships.FindAsync([orgId, invitedUserId.ToString()], TestContext.Current.CancellationToken);
        Assert.NotNull(membership);
        Assert.Equal("Owner", membership.Role);
    }

    [Fact]
    public async Task ChangeRole_ByNonOwner_ReturnsForbidden()
    {
        var orgId = await CreateOrgAsync();
        var invitedUserId = Guid.NewGuid();

        await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(invitedUserId, "Member"),
            TestContext.Current.CancellationToken);

        var memberClient = factory.CreateAuthenticatedClient(userId: invitedUserId);

        var response = await memberClient.PatchAsJsonAsync(
            $"/api/organizations/{orgId}/members/{invitedUserId}/role",
            new ChangeMemberRoleRequest("Owner"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_InvalidRole_ReturnsBadRequest()
    {
        var orgId = await CreateOrgAsync();
        var invitedUserId = Guid.NewGuid();

        await _client.PostAsJsonAsync(
            $"/api/organizations/{orgId}/members",
            new InviteMemberRequest(invitedUserId, "Member"),
            TestContext.Current.CancellationToken);

        var response = await _client.PatchAsJsonAsync(
            $"/api/organizations/{orgId}/members/{invitedUserId}/role",
            new ChangeMemberRoleRequest("SuperOwner"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_MembershipNotFound_ReturnsNotFound()
    {
        var orgId = await CreateOrgAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/organizations/{orgId}/members/{Guid.NewGuid()}/role",
            new ChangeMemberRoleRequest("Owner"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_LastOwnerDemotion_ReturnsBadRequest()
    {
        var orgId = await CreateOrgAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/organizations/{orgId}/members/{factory.DefaultTestUserId}/role",
            new ChangeMemberRoleRequest("Member"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
