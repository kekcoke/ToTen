using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Models;
using ToTen.Api.Data;
using ToTen.Api.Features.Items.CreateItem;
using ToTen.Api.Features.Items.UpdateItem;
using ToTen.Api.Features.Items.GetItem;
using ToTen.Api.Features.Items.GetItems;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Items;

public class ItemsEndpointsTests(ToTenWebApplicationFactory factory) : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ToTenWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetItems_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            Description = "Test Description",
            OwnerId = Guid.NewGuid().ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.GetAsync("/api/items", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<GetItemResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains(result, i => i.Name == "Test Item");
    }

    [Fact]
    public async Task GetItem_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Single Item",
            Description = "Details",
            OwnerId = Guid.NewGuid().ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.GetAsync($"/api/items/{item.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetItemResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Single Item", result.Name);
    }

    [Fact]
    public async Task CreateItem_ReturnsCreated()
    {
        // Arrange
        var request = new CreateItemRequest("New Item", "New Description", Guid.NewGuid());

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GetItemResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("New Item", result.Name);
    }

    [Fact]
    public async Task UpdateItem_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old Description",
            OwnerId = Guid.NewGuid().ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var request = new UpdateItemRequest("New Name", "Updated Description", Guid.NewGuid());

        // Act
        var response = await _client.PutAsJsonAsync($"/api/items/{item.Id}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        var updatedItem = await context.InventoryItems.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.Equal("New Name", updatedItem?.Name);
    }

    [Fact]
    public async Task DeleteItem_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            Description = "To Delete Description",
            OwnerId = Guid.NewGuid().ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.DeleteAsync($"/api/items/{item.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var deletedItem = await context.InventoryItems.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.Null(deletedItem);
    }
}
