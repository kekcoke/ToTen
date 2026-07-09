using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rebus.Bus;
using ToTen.Api.Models;
using ToTen.Api.Data;
using ToTen.Api.Features.Items.CreateItem;
using ToTen.Api.Features.Items.UpdateItem;
using ToTen.Api.Features.Items.GetItem;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Contracts.Events;

namespace ToTen.Api.IntegrationTests.Items;

public class ItemsEndpointsTests(ToTenWebApplicationFactory factory) : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ToTenWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetItems_ReturnsOk()
    {
        var categoryId = _factory.GetSeedCategoryId();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            Description = "Test Description",
            CategoryId = categoryId,
            OwnerId = _factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync("/items", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<GetItemResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains(result, i => i.Name == "Test Item");
    }

    [Fact]
    public async Task GetItem_ReturnsOk()
    {
        var categoryId = _factory.GetSeedCategoryId();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Single Item",
            Description = "Details",
            CategoryId = categoryId,
            OwnerId = _factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync($"/items/{item.Id}", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetItemResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Single Item", result.Name);
    }

    [Fact]
    public async Task CreateItem_ReturnsCreated()
    {
        var categoryId = _factory.GetSeedCategoryId();
        var request = new CreateItemRequest("New Item", "New Description", categoryId);

        var response = await _client.PostAsJsonAsync("/items", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<InventoryItem>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(_factory.DefaultTestUserId.ToString(), created.OwnerId);
    }

    [Fact]
    public async Task CreateItem_InvalidRequest_ReturnsBadRequest()
    {
        var request = new CreateItemRequest("", "New Description", Guid.Empty);

        var response = await _client.PostAsJsonAsync("/items", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_ReturnsNoContent()
    {
        var categoryId = _factory.GetSeedCategoryId();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old Description",
            CategoryId = categoryId,
            OwnerId = _factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var request = new UpdateItemRequest("New Name", "Updated Description", categoryId);

        var response = await _client.PutAsJsonAsync($"/items/{item.Id}", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updatedItem = await context.InventoryItems.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.Equal("New Name", updatedItem?.Name);
    }

    [Fact]
    public async Task DeleteItem_ReturnsNoContent()
    {
        var categoryId = _factory.GetSeedCategoryId();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            Description = "To Delete Description",
            CategoryId = categoryId,
            OwnerId = _factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.DeleteAsync($"/items/{item.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var verifyCtx = _factory.CreateDbContext();
        var deletedItem = await verifyCtx.InventoryItems.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.Null(deletedItem);

        var bus = _factory.Services.GetRequiredService<IBus>();
        await bus.Received(1).Publish(
            Arg.Is<ItemDeletedEvent>(e => e.ItemId == item.Id),
            Arg.Any<IDictionary<string, string>>());
    }
}
