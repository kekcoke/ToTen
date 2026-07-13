using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.Features.Storage.UpdateLocation;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Storage;

public class LocationEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

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

    private async Task<Location> SeedLocationAsync(string ownerId, Guid? organizationId = null, string name = "Seeded Location")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var location = new Location
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            OrganizationId = organizationId
        };
        context.Locations.Add(location);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return location;
    }

    [Fact]
    public async Task CreateLocation_CallerNotOrgMember_ReturnsForbidden()
    {
        var orgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString());

        var response = await _client.PostAsJsonAsync(
            "/api/locations",
            new CreateLocationRequest("Someone Else's Org Location", null, null, orgId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateLocation_DuplicateName_ReturnsConflict()
    {
        var orgId = await CreateOrgWithMemberAsync();
        await SeedLocationAsync(factory.DefaultTestUserId.ToString(), orgId, "Duplicate Name");

        var response = await _client.PostAsJsonAsync(
            "/api/locations",
            new CreateLocationRequest("Duplicate Name", null, null, orgId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetLocation_ReturnsOk()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());

        var response = await _client.GetAsync($"/api/locations/{location.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(location.Id, result.Id);
    }

    [Fact]
    public async Task GetLocation_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/locations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLocation_Forbidden_ForNonOwnerNonMember()
    {
        var location = await SeedLocationAsync(Guid.NewGuid().ToString());

        var response = await _client.GetAsync($"/api/locations/{location.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLocations_ReturnsOnlyCallerAccessible_Paginated()
    {
        var callerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId: callerId);

        var callerOrgId = await CreateOrgWithMemberAsync(userId: callerId.ToString());
        var otherOrgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString());

        var loc1 = await SeedLocationAsync(callerId.ToString(), callerOrgId, "Loc 1");
        var loc2 = await SeedLocationAsync(callerId.ToString(), callerOrgId, "Loc 2");
        var loc3 = await SeedLocationAsync(callerId.ToString(), callerOrgId, "Loc 3");
        var otherLoc = await SeedLocationAsync(Guid.NewGuid().ToString(), otherOrgId, "Other Org Loc");

        var page1Response = await client.GetAsync("/api/locations?page=1&pageSize=2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        Assert.Equal("3", page1Response.Headers.GetValues("X-Total-Count").Single());
        var page1 = await page1Response.Content.ReadFromJsonAsync<List<LocationResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page1);
        Assert.Equal(2, page1.Count);
        Assert.DoesNotContain(page1, l => l.Id == otherLoc.Id);

        var page2Response = await client.GetAsync("/api/locations?page=2&pageSize=2", TestContext.Current.CancellationToken);
        var page2 = await page2Response.Content.ReadFromJsonAsync<List<LocationResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page2);
        Assert.Single(page2);

        var allReturnedIds = page1.Concat(page2).Select(l => l.Id).ToList();
        Assert.Contains(loc1.Id, allReturnedIds);
        Assert.Contains(loc2.Id, allReturnedIds);
        Assert.Contains(loc3.Id, allReturnedIds);
    }

    [Fact]
    public async Task UpdateLocation_ReturnsNoContent()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{location.Id}",
            new UpdateLocationRequest("Renamed Location", null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/locations/{location.Id}", TestContext.Current.CancellationToken);
        var result = await getResponse.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Renamed Location", result.Name);
    }

    [Fact]
    public async Task UpdateLocation_Forbidden_ForNonOwnerNonMember()
    {
        var location = await SeedLocationAsync(Guid.NewGuid().ToString());

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{location.Id}",
            new UpdateLocationRequest("Renamed", null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLocation_DuplicateName_ReturnsConflict()
    {
        var orgId = await CreateOrgWithMemberAsync();
        await SeedLocationAsync(factory.DefaultTestUserId.ToString(), orgId, "Taken Name");
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString(), orgId, "Original Name");

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{location.Id}",
            new UpdateLocationRequest("Taken Name", null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLocation_NotFound_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{Guid.NewGuid()}",
            new UpdateLocationRequest("Anything", null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLocation_ReturnsNoContent()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());

        var response = await _client.DeleteAsync($"/api/locations/{location.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLocation_Forbidden_ForNonOwnerNonMember()
    {
        var location = await SeedLocationAsync(Guid.NewGuid().ToString());

        var response = await _client.DeleteAsync($"/api/locations/{location.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLocation_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/locations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLocation_ReferencedByBox_ReturnsConflict()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        ctx.Boxes.Add(new Box
        {
            Id = Guid.NewGuid(),
            Name = "Blocking Box",
            LocationId = location.Id,
            OwnerId = factory.DefaultTestUserId.ToString()
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.DeleteAsync($"/api/locations/{location.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLocation_ReferencedByManifest_ReturnsConflict()
    {
        var orgId = await CreateOrgWithMemberAsync();
        var source = await SeedLocationAsync(factory.DefaultTestUserId.ToString(), orgId, "Manifest Source");
        var dest = await SeedLocationAsync(factory.DefaultTestUserId.ToString(), orgId, "Manifest Dest");

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        ctx.Manifests.Add(new Manifest
        {
            Id = Guid.NewGuid(),
            SourceLocationId = source.Id,
            DestinationLocationId = dest.Id,
            Status = ManifestStatus.Draft,
            OrganizationId = orgId
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.DeleteAsync($"/api/locations/{source.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LocationEndpoints_NoAuth_Return401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.GetAsync($"/api/locations/{id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.GetAsync("/api/locations", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.PutAsJsonAsync($"/api/locations/{id}", new UpdateLocationRequest("X", null, null), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.DeleteAsync($"/api/locations/{id}", TestContext.Current.CancellationToken)).StatusCode);
    }
}
