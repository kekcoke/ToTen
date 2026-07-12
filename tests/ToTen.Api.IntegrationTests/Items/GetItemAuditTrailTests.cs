using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Items.GetItemAuditTrail;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Items;

public class GetItemAuditTrailTests(ToTenWebApplicationFactory factory) : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ToTenWebApplicationFactory _factory = factory;

    private async Task<InventoryItem> SeedItemAsync(ToTenContext context)
    {
        var categoryId = _factory.GetSeedCategoryId();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Audited Item",
            Description = "Description",
            CategoryId = categoryId,
            OwnerId = _factory.DefaultTestUserId.ToString(),
            LastUpdatedBy = "test@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    private static AuditLogEntry NewEntry(Guid itemId, string eventType, DateTimeOffset occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        ItemId = itemId,
        ManifestId = null,
        ActorId = "actor-1",
        OccurredAt = occurredAt,
        RecordedAt = occurredAt,
        Payload = JsonDocument.Parse("""{"note":"test"}""")
    };

    [Fact]
    public async Task GetItemAuditTrail_ReturnsOk_OrderedNewestFirst()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(context);

        var now = DateTimeOffset.UtcNow;
        context.AuditLogEntries.AddRange(
            NewEntry(item.Id, "ItemMoved", now.AddMinutes(-10)),
            NewEntry(item.Id, "ItemListed", now.AddMinutes(-1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync($"/items/{item.Id}/audit", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AuditLogEntryResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("ItemListed", result[0].EventType);
        Assert.Equal("ItemMoved", result[1].EventType);
    }

    [Fact]
    public async Task GetItemAuditTrail_ReturnsEmptyList_WhenNoHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(context);

        var response = await _client.GetAsync($"/items/{item.Id}/audit", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AuditLogEntryResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Empty(result);

        Assert.True(response.Headers.TryGetValues("X-Total-Count", out var totalCountValues));
        Assert.Equal("0", totalCountValues.Single());
    }

    [Fact]
    public async Task GetItemAuditTrail_RespectsPagination()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(context);

        var now = DateTimeOffset.UtcNow;
        context.AuditLogEntries.AddRange(
            NewEntry(item.Id, "ItemMoved", now.AddMinutes(-3)),
            NewEntry(item.Id, "ItemListed", now.AddMinutes(-2)),
            NewEntry(item.Id, "ItemTransferred", now.AddMinutes(-1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync($"/items/{item.Id}/audit?page=1&pageSize=1", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AuditLogEntryResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);

        Assert.True(response.Headers.TryGetValues("X-Total-Count", out var totalCountValues));
        Assert.Equal("3", totalCountValues.Single());
    }

    [Fact]
    public async Task GetItemAuditTrail_ReturnsNotFound_ForNonexistentItem()
    {
        var response = await _client.GetAsync($"/items/{Guid.NewGuid()}/audit", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetItemAuditTrail_ReturnsForbidden_ForNonOwner()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(context);

        var otherUserClient = _factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await otherUserClient.GetAsync($"/items/{item.Id}/audit", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
