using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Manifests.AssociateBoxes;
using ToTen.Api.Features.Manifests.CreateManifest;
using ToTen.Api.Features.Manifests.GenerateQR;
using ToTen.Api.Features.Manifests.UpdateManifestStatus;
using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Manifests;

public class ManifestsEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid sourceId, Guid destId)> CreateLocationsAsync()
    {
        var src = await _client.PostAsJsonAsync("/api/locations",
            new CreateLocationRequest("Source", null, null, null),
            TestContext.Current.CancellationToken);
        var dest = await _client.PostAsJsonAsync("/api/locations",
            new CreateLocationRequest("Destination", null, null, null),
            TestContext.Current.CancellationToken);
        var srcLoc = await src.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        var destLoc = await dest.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        return (srcLoc!.Id, destLoc!.Id);
    }

    /// <summary>
    /// Creates an organization and enrolls the given user (default: the default test user) as a
    /// member with the given role (default: "Member").
    /// </summary>
    private async Task<Guid> CreateOrgWithMemberAsync(string? userId = null, string role = "Member")
    {
        var orgId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        context.Organizations.Add(new Organization { Id = orgId, Name = "Test Org", Type = "Household" });
        context.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = orgId,
            UserId = userId ?? factory.DefaultTestUserId.ToString(),
            Role = role
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return orgId;
    }

    private async Task<Manifest> SeedManifestAsync(Guid organizationId, Guid sourceLocationId, Guid destinationLocationId, ManifestStatus status = ManifestStatus.Draft)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var manifest = new Manifest
        {
            Id = Guid.NewGuid(),
            SourceLocationId = sourceLocationId,
            DestinationLocationId = destinationLocationId,
            Status = status,
            OrganizationId = organizationId
        };
        context.Manifests.Add(manifest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return manifest;
    }

    [Fact]
    public async Task CreateManifest_ReturnsCreated_WithDraftStatus()
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(srcId, destId, orgId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ManifestResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(ManifestStatus.Draft, result.Status);
        Assert.Equal(srcId, result.SourceLocationId);
        Assert.Equal(destId, result.DestinationLocationId);
        Assert.Equal(orgId, result.OrganizationId);
    }

    [Fact]
    public async Task CreateManifest_InvalidRequest_ReturnsBadRequest()
    {
        var orgId = await CreateOrgWithMemberAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(Guid.Empty, Guid.Empty, orgId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateManifest_MissingOrganizationId_ReturnsBadRequest()
    {
        var (srcId, destId) = await CreateLocationsAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(srcId, destId, Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateManifest_CallerNotOrgMember_ReturnsForbidden()
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString());

        var response = await _client.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(srcId, destId, orgId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateManifest_NoAuth_Returns401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenerateQR_OwnedBox_ReturnsOkWithUrl()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var location = new ToTen.Api.Models.Location
        {
            Id = Guid.NewGuid(),
            Name = "QR Test Location",
            OwnerId = factory.DefaultTestUserId.ToString()
        };
        var box = new Box
        {
            Id = Guid.NewGuid(),
            Name = "QR Box",
            LocationId = location.Id,
            OwnerId = factory.DefaultTestUserId.ToString()
        };
        ctx.Locations.Add(location);
        ctx.Boxes.Add(box);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsync(
            $"/api/boxes/{box.Id}/qr",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GenerateQRResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains("test.blob.core.windows.net", result.QrCodeUrl);
    }

    [Fact]
    public async Task AssociateBoxes_WithOwnedBoxes_ReturnsNoContent()
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync();

        var manifestResponse = await _client.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(srcId, destId, orgId),
            TestContext.Current.CancellationToken);
        var manifest = await manifestResponse.Content.ReadFromJsonAsync<ManifestResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(manifest);

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var box = new Box
        {
            Id = Guid.NewGuid(),
            Name = "Assoc Box",
            LocationId = srcId,
            OwnerId = factory.DefaultTestUserId.ToString()
        };
        ctx.Boxes.Add(box);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync(
            $"/api/manifests/{manifest.Id}/boxes",
            new AssociateBoxesRequest([box.Id]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GenerateQR_UnownedBox_ReturnsForbidden()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var otherUserId = Guid.NewGuid();
        var location = new ToTen.Api.Models.Location
        {
            Id = Guid.NewGuid(),
            Name = "Other QR Location",
            OwnerId = otherUserId.ToString()
        };
        var box = new Box
        {
            Id = Guid.NewGuid(),
            Name = "Other User Box",
            LocationId = location.Id,
            OwnerId = otherUserId.ToString()
        };
        ctx.Locations.Add(location);
        ctx.Boxes.Add(box);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsync(
            $"/api/boxes/{box.Id}/qr",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetManifest_ByOrgMember_ReturnsOk()
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync();
        var manifest = await SeedManifestAsync(orgId, srcId, destId);

        var response = await _client.GetAsync($"/api/manifests/{manifest.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ManifestResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(manifest.Id, result.Id);
    }

    [Fact]
    public async Task GetManifest_ByNonMember_ReturnsForbidden()
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString());
        var manifest = await SeedManifestAsync(orgId, srcId, destId);

        var response = await _client.GetAsync($"/api/manifests/{manifest.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetManifest_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/manifests/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetManifests_ReturnsOnlyCallerOrgManifests_Paginated()
    {
        // Uses an isolated caller identity (not factory.DefaultTestUserId) so this test's
        // X-Total-Count assertions aren't polluted by other tests in this class fixture that
        // also enroll the shared default user into their own orgs against the same Postgres
        // container.
        var callerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId: callerId);

        var (srcId, destId) = await CreateLocationsAsync();
        var callerOrgId = await CreateOrgWithMemberAsync(userId: callerId.ToString());
        var otherOrgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString());

        var callerManifest1 = await SeedManifestAsync(callerOrgId, srcId, destId);
        var callerManifest2 = await SeedManifestAsync(callerOrgId, srcId, destId);
        var callerManifest3 = await SeedManifestAsync(callerOrgId, srcId, destId);
        var otherManifest = await SeedManifestAsync(otherOrgId, srcId, destId);

        var page1Response = await client.GetAsync("/api/manifests?page=1&pageSize=2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        Assert.Equal("3", page1Response.Headers.GetValues("X-Total-Count").Single());
        var page1 = await page1Response.Content.ReadFromJsonAsync<List<ManifestResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page1);
        Assert.Equal(2, page1.Count);
        Assert.DoesNotContain(page1, m => m.Id == otherManifest.Id);

        var page2Response = await client.GetAsync("/api/manifests?page=2&pageSize=2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);
        var page2 = await page2Response.Content.ReadFromJsonAsync<List<ManifestResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page2);
        Assert.Single(page2);

        var allReturnedIds = page1.Concat(page2).Select(m => m.Id).ToList();
        Assert.Contains(callerManifest1.Id, allReturnedIds);
        Assert.Contains(callerManifest2.Id, allReturnedIds);
        Assert.Contains(callerManifest3.Id, allReturnedIds);
    }

    [Theory]
    [InlineData(ManifestStatus.Draft, ManifestStatus.Pending)]
    [InlineData(ManifestStatus.Pending, ManifestStatus.InTransit)]
    [InlineData(ManifestStatus.InTransit, ManifestStatus.Received)]
    public async Task UpdateManifestStatus_ValidForwardTransitions_ReturnsOk(ManifestStatus from, ManifestStatus to)
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync(role: "Owner");
        var manifest = await SeedManifestAsync(orgId, srcId, destId, from);

        var response = await _client.PatchAsJsonAsync(
            $"/api/manifests/{manifest.Id}/status",
            new UpdateManifestStatusRequest(to),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ManifestResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(to, result.Status);
    }

    [Theory]
    [InlineData(ManifestStatus.Draft)]
    [InlineData(ManifestStatus.Pending)]
    [InlineData(ManifestStatus.InTransit)]
    public async Task UpdateManifestStatus_ValidCancellations_ReturnsOk(ManifestStatus from)
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync(role: "Owner");
        var manifest = await SeedManifestAsync(orgId, srcId, destId, from);

        var response = await _client.PatchAsJsonAsync(
            $"/api/manifests/{manifest.Id}/status",
            new UpdateManifestStatusRequest(ManifestStatus.Cancelled),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ManifestResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(ManifestStatus.Cancelled, result.Status);
    }

    [Theory]
    [InlineData(ManifestStatus.Received, ManifestStatus.Draft)]
    [InlineData(ManifestStatus.Draft, ManifestStatus.Received)]
    [InlineData(ManifestStatus.Cancelled, ManifestStatus.Pending)]
    [InlineData(ManifestStatus.Received, ManifestStatus.Cancelled)]
    public async Task UpdateManifestStatus_InvalidTransitions_ReturnsBadRequest(ManifestStatus from, ManifestStatus to)
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync(role: "Owner");
        var manifest = await SeedManifestAsync(orgId, srcId, destId, from);

        var response = await _client.PatchAsJsonAsync(
            $"/api/manifests/{manifest.Id}/status",
            new UpdateManifestStatusRequest(to),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateManifestStatus_ByNonOwnerMember_ReturnsForbidden()
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync(role: "Member");
        var manifest = await SeedManifestAsync(orgId, srcId, destId);

        var response = await _client.PatchAsJsonAsync(
            $"/api/manifests/{manifest.Id}/status",
            new UpdateManifestStatusRequest(ManifestStatus.Pending),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateManifestStatus_ByNonMember_ReturnsForbidden()
    {
        var (srcId, destId) = await CreateLocationsAsync();
        var orgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString(), role: "Owner");
        var manifest = await SeedManifestAsync(orgId, srcId, destId);

        var response = await _client.PatchAsJsonAsync(
            $"/api/manifests/{manifest.Id}/status",
            new UpdateManifestStatusRequest(ManifestStatus.Pending),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateManifestStatus_ManifestNotFound_Returns404()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/manifests/{Guid.NewGuid()}/status",
            new UpdateManifestStatusRequest(ManifestStatus.Pending),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
