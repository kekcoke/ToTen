using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.GetTransactions;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Marketplace;

public class TransactionsEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<InventoryItem> SeedItemAsync(ToTenContext ctx, string ownerId)
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Sold Item",
            Description = "For transaction test",
            CategoryId = factory.GetSeedCategoryId(),
            OwnerId = ownerId,
            LastUpdatedBy = "test"
        };
        ctx.InventoryItems.Add(item);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    private async Task<Transaction> SeedTransactionAsync(
        ToTenContext ctx, Guid itemId, string sellerId, string buyerId, decimal amount)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            InventoryItemId = itemId,
            SellerId = sellerId,
            BuyerId = buyerId,
            Amount = amount,
            Timestamp = DateTimeOffset.UtcNow
        };
        ctx.Transactions.Add(transaction);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return transaction;
    }

    [Fact]
    public async Task GetTransactions_ReturnsOnlyCallersTransactions_WithTotalCount()
    {
        var me = factory.DefaultTestUserId.ToString();
        var other = Guid.NewGuid().ToString();

        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
            var item = await SeedItemAsync(ctx, other);
            await SeedTransactionAsync(ctx, item.Id, sellerId: me, buyerId: other, amount: 50m);
            await SeedTransactionAsync(ctx, item.Id, sellerId: other, buyerId: me, amount: 75m);
            // Unrelated transaction between two other parties — must not appear.
            await SeedTransactionAsync(ctx, item.Id, sellerId: other, buyerId: Guid.NewGuid().ToString(), amount: 99m);
        }

        var response = await _client.GetAsync("/api/transactions", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(results);
        // Every returned row involves the caller; the two seeded rows are present and the
        // unrelated one (99m, between two other parties) is not. Avoid asserting an exact
        // total, since this class shares one database across tests.
        Assert.All(results, t => Assert.True(t.SellerId == me || t.BuyerId == me));
        Assert.Contains(results, t => t.Amount == 50m);
        Assert.Contains(results, t => t.Amount == 75m);
        Assert.DoesNotContain(results, t => t.Amount == 99m);
        // X-Total-Count is internally consistent with the returned page (page fits in pageSize).
        Assert.Equal(results.Count.ToString(), response.Headers.GetValues("X-Total-Count").Single());
    }

    [Fact]
    public async Task GetTransactionById_AsParty_ReturnsTransactionWithLineage()
    {
        var me = factory.DefaultTestUserId.ToString();
        var seller = Guid.NewGuid().ToString();
        Guid transactionId;

        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
            var item = await SeedItemAsync(ctx, me);
            var transaction = await SeedTransactionAsync(ctx, item.Id, sellerId: seller, buyerId: me, amount: 120m);
            transactionId = transaction.Id;

            ctx.ItemLineages.Add(new ItemLineage
            {
                Id = Guid.NewGuid(),
                InventoryItemId = item.Id,
                Action = "OwnershipTransfer",
                TransactionId = transactionId,
                ChangedByUserId = me,
                Timestamp = DateTimeOffset.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _client.GetAsync($"/api/transactions/{transactionId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransactionDetailResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(transactionId, result.Id);
        Assert.Single(result.Lineage);
        Assert.Equal("OwnershipTransfer", result.Lineage[0].Action);
    }

    [Fact]
    public async Task GetTransactionById_AsNonParty_ReturnsForbidden()
    {
        var seller = Guid.NewGuid().ToString();
        var buyer = Guid.NewGuid().ToString();
        Guid transactionId;

        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
            var item = await SeedItemAsync(ctx, seller);
            var transaction = await SeedTransactionAsync(ctx, item.Id, sellerId: seller, buyerId: buyer, amount: 40m);
            transactionId = transaction.Id;
        }

        var response = await _client.GetAsync($"/api/transactions/{transactionId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactionById_Missing_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/transactions/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
