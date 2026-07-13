using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Storage.CreateBox;
using ToTen.Api.Features.Storage.UpdateBox;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Storage;

public class BoxEndpointsTests(ToTenWebApplicationFactory factory)
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

    private async Task<Box> SeedBoxAsync(string ownerId, Guid locationId, Guid? organizationId = null, string name = "Seeded Box")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var box = new Box
        {
            Id = Guid.NewGuid(),
            Name = name,
            LocationId = locationId,
            OwnerId = ownerId,
            OrganizationId = organizationId
        };
        context.Boxes.Add(box);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return box;
    }

    [Fact]
    public async Task CreateBox_ReturnsCreated()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());

        var response = await _client.PostAsJsonAsync(
            "/api/boxes",
            new CreateBoxRequest("New Box", location.Id, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BoxResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("New Box", result.Name);
        Assert.Equal(location.Id, result.LocationId);
    }

    [Fact]
    public async Task CreateBox_TargetLocationNotOwned_ReturnsForbidden()
    {
        var location = await SeedLocationAsync(Guid.NewGuid().ToString());

        var response = await _client.PostAsJsonAsync(
            "/api/boxes",
            new CreateBoxRequest("New Box", location.Id, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBox_TargetLocationNotFound_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/boxes",
            new CreateBoxRequest("New Box", Guid.NewGuid(), null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBox_CallerNotOrgMember_ReturnsForbidden()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());
        var orgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString());

        var response = await _client.PostAsJsonAsync(
            "/api/boxes",
            new CreateBoxRequest("New Box", location.Id, orgId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBox_ReturnsOk()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());
        var box = await SeedBoxAsync(factory.DefaultTestUserId.ToString(), location.Id);

        var response = await _client.GetAsync($"/api/boxes/{box.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BoxResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(box.Id, result.Id);
    }

    [Fact]
    public async Task GetBox_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/boxes/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBox_Forbidden_ForNonOwnerNonMember()
    {
        var otherUserId = Guid.NewGuid().ToString();
        var location = await SeedLocationAsync(otherUserId);
        var box = await SeedBoxAsync(otherUserId, location.Id);

        var response = await _client.GetAsync($"/api/boxes/{box.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoxes_ReturnsOnlyCallerAccessible_Paginated()
    {
        var callerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId: callerId);

        var callerOrgId = await CreateOrgWithMemberAsync(userId: callerId.ToString());
        var otherOrgId = await CreateOrgWithMemberAsync(userId: Guid.NewGuid().ToString());

        var callerLocation = await SeedLocationAsync(callerId.ToString(), callerOrgId);
        var otherLocation = await SeedLocationAsync(Guid.NewGuid().ToString(), otherOrgId);

        var box1 = await SeedBoxAsync(callerId.ToString(), callerLocation.Id, callerOrgId, "Box 1");
        var box2 = await SeedBoxAsync(callerId.ToString(), callerLocation.Id, callerOrgId, "Box 2");
        var box3 = await SeedBoxAsync(callerId.ToString(), callerLocation.Id, callerOrgId, "Box 3");
        var otherBox = await SeedBoxAsync(Guid.NewGuid().ToString(), otherLocation.Id, otherOrgId, "Other Box");

        var page1Response = await client.GetAsync("/api/boxes?page=1&pageSize=2", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        Assert.Equal("3", page1Response.Headers.GetValues("X-Total-Count").Single());
        var page1 = await page1Response.Content.ReadFromJsonAsync<List<BoxResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page1);
        Assert.Equal(2, page1.Count);
        Assert.DoesNotContain(page1, b => b.Id == otherBox.Id);

        var page2Response = await client.GetAsync("/api/boxes?page=2&pageSize=2", TestContext.Current.CancellationToken);
        var page2 = await page2Response.Content.ReadFromJsonAsync<List<BoxResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page2);
        Assert.Single(page2);

        var allReturnedIds = page1.Concat(page2).Select(b => b.Id).ToList();
        Assert.Contains(box1.Id, allReturnedIds);
        Assert.Contains(box2.Id, allReturnedIds);
        Assert.Contains(box3.Id, allReturnedIds);
    }

    [Fact]
    public async Task UpdateBox_ReturnsNoContent()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());
        var box = await SeedBoxAsync(factory.DefaultTestUserId.ToString(), location.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/boxes/{box.Id}",
            new UpdateBoxRequest("Renamed Box", location.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/boxes/{box.Id}", TestContext.Current.CancellationToken);
        var result = await getResponse.Content.ReadFromJsonAsync<BoxResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Renamed Box", result.Name);
    }

    [Fact]
    public async Task UpdateBox_Forbidden_ForNonOwnerNonMember()
    {
        var otherUserId = Guid.NewGuid().ToString();
        var location = await SeedLocationAsync(otherUserId);
        var box = await SeedBoxAsync(otherUserId, location.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/boxes/{box.Id}",
            new UpdateBoxRequest("Renamed", location.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBox_MoveToUnownedLocation_ReturnsForbidden()
    {
        var ownLocation = await SeedLocationAsync(factory.DefaultTestUserId.ToString());
        var box = await SeedBoxAsync(factory.DefaultTestUserId.ToString(), ownLocation.Id);
        var unownedLocation = await SeedLocationAsync(Guid.NewGuid().ToString());

        var response = await _client.PutAsJsonAsync(
            $"/api/boxes/{box.Id}",
            new UpdateBoxRequest(box.Name, unownedLocation.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBox_NotFound_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/boxes/{Guid.NewGuid()}",
            new UpdateBoxRequest("Anything", Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBox_ReturnsNoContent()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());
        var box = await SeedBoxAsync(factory.DefaultTestUserId.ToString(), location.Id);

        var response = await _client.DeleteAsync($"/api/boxes/{box.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBox_Forbidden_ForNonOwnerNonMember()
    {
        var otherUserId = Guid.NewGuid().ToString();
        var location = await SeedLocationAsync(otherUserId);
        var box = await SeedBoxAsync(otherUserId, location.Id);

        var response = await _client.DeleteAsync($"/api/boxes/{box.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBox_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/boxes/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBox_ReferencedByItem_ReturnsConflict()
    {
        var location = await SeedLocationAsync(factory.DefaultTestUserId.ToString());
        var box = await SeedBoxAsync(factory.DefaultTestUserId.ToString(), location.Id);
        var categoryId = factory.GetSeedCategoryId();

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        ctx.InventoryItems.Add(new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Blocking Item",
            Description = "Test",
            CategoryId = categoryId,
            OwnerId = factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com",
            LocationId = location.Id,
            BoxId = box.Id
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.DeleteAsync($"/api/boxes/{box.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task BoxEndpoints_NoAuth_Return401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.PostAsJsonAsync("/api/boxes", new CreateBoxRequest("X", id, null), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.GetAsync($"/api/boxes/{id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.GetAsync("/api/boxes", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.PutAsJsonAsync($"/api/boxes/{id}", new UpdateBoxRequest("X", id), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthClient.DeleteAsync($"/api/boxes/{id}", TestContext.Current.CancellationToken)).StatusCode);
    }
}
