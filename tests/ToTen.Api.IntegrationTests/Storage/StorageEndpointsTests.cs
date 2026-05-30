using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.Features.Storage.MoveItem;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Storage;

public class StorageEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateLocation_ReturnsCreated()
    {
        var request = new CreateLocationRequest("Garage", null, null, null);

        var response = await _client.PostAsJsonAsync("/api/locations", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Garage", result.Name);
    }

    [Fact]
    public async Task CreateLocation_WithGeometry_CoordinatesRoundTrip()
    {
        var request = new CreateLocationRequest("Shed", 40.7128, -74.0060, null);

        var response = await _client.PostAsJsonAsync("/api/locations", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.NotNull(result.Latitude);
        Assert.NotNull(result.Longitude);
        Assert.InRange(result.Latitude.Value, 40.7127, 40.7129);
        Assert.InRange(result.Longitude.Value, -74.0061, -74.0059);
    }

    [Fact]
    public async Task CreateLocation_NoAuth_Returns401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.PostAsJsonAsync(
            "/api/locations",
            new CreateLocationRequest("Secret", null, null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MoveItem_ToOwnedLocation_ReturnsOk()
    {
        var categoryId = factory.GetSeedCategoryId();

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Moveable Item",
            Description = "Test",
            CategoryId = categoryId,
            OwnerId = factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com"
        };
        ctx.InventoryItems.Add(item);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var locationResponse = await _client.PostAsJsonAsync(
            "/api/locations",
            new CreateLocationRequest("Target Location", null, null, null),
            TestContext.Current.CancellationToken);
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(location);

        var response = await _client.PostAsJsonAsync(
            $"/api/items/{item.Id}/move",
            new MoveItemRequest(location.Id, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MoveItemResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(location.Id, result.NewLocationId);
    }

    [Fact]
    public async Task MoveItem_ToUnownedLocation_ReturnsForbidden()
    {
        var categoryId = factory.GetSeedCategoryId();
        var differentUserId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "My Item",
            Description = "Ownership test",
            CategoryId = categoryId,
            OwnerId = factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com"
        };
        var unownedLocation = new ToTen.Api.Models.Location
        {
            Id = Guid.NewGuid(),
            Name = "Other User Location",
            OwnerId = differentUserId.ToString()
        };
        ctx.InventoryItems.Add(item);
        ctx.Locations.Add(unownedLocation);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync(
            $"/api/items/{item.Id}/move",
            new MoveItemRequest(unownedLocation.Id, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MoveItem_NoAuth_Returns401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.PostAsJsonAsync(
            $"/api/items/{Guid.NewGuid()}/move",
            new MoveItemRequest(null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
