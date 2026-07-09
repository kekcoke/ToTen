using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Items.CreateItem;
using ToTen.Api.Features.Items.GetItem;
using ToTen.Api.Features.Items.UpdateItem;
using ToTen.Api.Models;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Multi-user ownership/IDOR tests for the Items feature (audit findings 1.2-1.4).
/// Validates that GetItem/UpdateItem/DeleteItem/GetItems correctly gate on ownership
/// and org membership via ResourceAuthorizationHandler, instead of any authenticated
/// user being able to read/modify/delete anyone else's items.
/// </summary>
public class ItemAccessTests(ToTenWebApplicationFactory factory) : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<InventoryItem> SeedItemAsync(string ownerId, Guid? organizationId = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var categoryId = factory.GetSeedCategoryId();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Owned Item",
            Description = "Description",
            CategoryId = categoryId,
            OwnerId = ownerId,
            OrganizationId = organizationId,
            LastUpdatedBy = "seed@example.com"
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task GetItem_ByNonOwner_ReturnsForbidden()
    {
        var item = await SeedItemAsync(factory.DefaultTestUserId.ToString());
        var strangerClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await strangerClient.GetAsync($"/items/{item.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_ByNonOwner_ReturnsForbidden_AndItemUnchanged()
    {
        var item = await SeedItemAsync(factory.DefaultTestUserId.ToString());
        var strangerClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await strangerClient.PutAsJsonAsync(
            $"/items/{item.Id}",
            new UpdateItemRequest("Hijacked", "Hijacked Description", item.CategoryId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verifyCtx = factory.CreateDbContext();
        var unchanged = await verifyCtx.InventoryItems.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.Equal("Owned Item", unchanged?.Name);
    }

    [Fact]
    public async Task DeleteItem_ByNonOwner_ReturnsForbidden_AndItemNotDeleted()
    {
        var item = await SeedItemAsync(factory.DefaultTestUserId.ToString());
        var strangerClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await strangerClient.DeleteAsync($"/items/{item.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verifyCtx = factory.CreateDbContext();
        var stillThere = await verifyCtx.InventoryItems.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(stillThere);
    }

    [Fact]
    public async Task GetItem_ByOrgMember_ReturnsOk()
    {
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ToTenContext>();
            context.Organizations.Add(new Organization { Id = orgId, Name = "Test Org", Type = "Household" });
            context.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = orgId,
                UserId = factory.DefaultTestUserId.ToString(),
                Role = "Member"
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var item = await SeedItemAsync(ownerId.ToString(), orgId);

        var response = await _client.GetAsync($"/items/{item.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_ByAdmin_ReturnsOk()
    {
        var item = await SeedItemAsync(Guid.NewGuid().ToString());
        var adminClient = factory.CreateAuthenticatedClient(roles: ["admin"]);

        var response = await adminClient.GetAsync($"/items/{item.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_OnlyReturnsOwnedAndOrgItems()
    {
        var ownItem = await SeedItemAsync(factory.DefaultTestUserId.ToString());
        var strangerItem = await SeedItemAsync(Guid.NewGuid().ToString());

        var response = await _client.GetAsync("/items", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<GetItemResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains(result, i => i.Id == ownItem.Id);
        Assert.DoesNotContain(result, i => i.Id == strangerItem.Id);
    }

    [Fact]
    public async Task GetItems_ByAdmin_ReturnsAllItems()
    {
        var strangerItem = await SeedItemAsync(Guid.NewGuid().ToString());
        var adminClient = factory.CreateAuthenticatedClient(roles: ["admin"]);

        var response = await adminClient.GetAsync("/items", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<GetItemResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains(result, i => i.Id == strangerItem.Id);
    }

    [Fact]
    public async Task CreateItem_OwnerIdMatchesAuthenticatedCaller()
    {
        var categoryId = factory.GetSeedCategoryId();

        var response = await _client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Mine", "Description", categoryId),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<InventoryItem>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(factory.DefaultTestUserId.ToString(), created.OwnerId);
        Assert.NotEqual("demo", created.OwnerId);
    }
}
