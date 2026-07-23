using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.RefundTransaction;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Marketplace;

public class RefundEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Seeds an item (owned by the buyer, i.e. post-sale) and a transaction with the default
    // test user as the seller, so the default client is authorized to issue refunds.
    private async Task<(Guid transactionId, Guid itemId, string sellerId, string buyerId)> SeedSaleAsync(decimal amount)
    {
        var sellerId = factory.DefaultTestUserId.ToString();
        var buyerId = Guid.NewGuid().ToString();

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Refundable Item",
            Description = "For refund test",
            CategoryId = factory.GetSeedCategoryId(),
            OwnerId = buyerId,
            LastUpdatedBy = "test"
        };
        ctx.InventoryItems.Add(item);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            SellerId = sellerId,
            BuyerId = buyerId,
            Amount = amount,
            Timestamp = DateTimeOffset.UtcNow
        };
        ctx.Transactions.Add(transaction);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (transaction.Id, item.Id, sellerId, buyerId);
    }

    private async Task<(string ownerId, int reversalCount)> ReadItemStateAsync(Guid itemId, Guid transactionId)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var owner = (await ctx.InventoryItems.FirstAsync(i => i.Id == itemId, TestContext.Current.CancellationToken)).OwnerId;
        var reversals = await ctx.ItemLineages
            .CountAsync(l => l.TransactionId == transactionId && l.Action == "RefundReversal",
                TestContext.Current.CancellationToken);
        return (owner, reversals);
    }

    [Fact]
    public async Task PartialRefund_LeavesOwnershipUnchanged_StatusCompleted()
    {
        var (transactionId, itemId, _, buyerId) = await SeedSaleAsync(100m);

        var response = await _client.PostAsJsonAsync(
            $"/api/transactions/{transactionId}/refunds",
            new RefundRequest(40m, "partial"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var refund = await response.Content.ReadFromJsonAsync<RefundResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(refund);
        Assert.Equal(40m, refund.Amount);
        Assert.Equal(RefundStatus.Completed, refund.Status);

        var (owner, reversals) = await ReadItemStateAsync(itemId, transactionId);
        Assert.Equal(buyerId, owner);   // partial refund does not move the item
        Assert.Equal(0, reversals);
    }

    [Fact]
    public async Task FullRefund_ReversesOwnershipToSeller()
    {
        var (transactionId, itemId, sellerId, _) = await SeedSaleAsync(100m);

        var response = await _client.PostAsJsonAsync(
            $"/api/transactions/{transactionId}/refunds",
            new RefundRequest(null, "full refund", Full: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var refund = await response.Content.ReadFromJsonAsync<RefundResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(refund);
        Assert.Equal(100m, refund.Amount);   // full short-circuits to the remaining balance

        var (owner, reversals) = await ReadItemStateAsync(itemId, transactionId);
        Assert.Equal(sellerId, owner);   // full refund returns the item to the seller
        Assert.Equal(1, reversals);
    }

    [Fact]
    public async Task CumulativePartials_ReachingTotal_ReverseOwnership()
    {
        var (transactionId, itemId, sellerId, buyerId) = await SeedSaleAsync(100m);

        var first = await _client.PostAsJsonAsync($"/api/transactions/{transactionId}/refunds",
            new RefundRequest(60m, "partial 1"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Ownership still with the buyer after the first partial.
        var afterFirst = await ReadItemStateAsync(itemId, transactionId);
        Assert.Equal(buyerId, afterFirst.ownerId);

        var second = await _client.PostAsJsonAsync($"/api/transactions/{transactionId}/refunds",
            new RefundRequest(40m, "partial 2"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        // Cumulative 100 == transaction total → treated as full → ownership reverses.
        var afterSecond = await ReadItemStateAsync(itemId, transactionId);
        Assert.Equal(sellerId, afterSecond.ownerId);
        Assert.Equal(1, afterSecond.reversalCount);
    }

    [Fact]
    public async Task OverRefund_ReturnsBadRequest()
    {
        var (transactionId, _, _, _) = await SeedSaleAsync(100m);

        var response = await _client.PostAsJsonAsync(
            $"/api/transactions/{transactionId}/refunds",
            new RefundRequest(150m, "too much"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refund_ByNonSeller_ReturnsForbidden()
    {
        // Transaction between two other parties; the default client is neither.
        Guid transactionId;
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Other Item",
                Description = "x",
                CategoryId = factory.GetSeedCategoryId(),
                OwnerId = Guid.NewGuid().ToString(),
                LastUpdatedBy = "test"
            };
            ctx.InventoryItems.Add(item);
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                InventoryItemId = item.Id,
                SellerId = Guid.NewGuid().ToString(),
                BuyerId = Guid.NewGuid().ToString(),
                Amount = 50m,
                Timestamp = DateTimeOffset.UtcNow
            };
            ctx.Transactions.Add(transaction);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            transactionId = transaction.Id;
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/transactions/{transactionId}/refunds",
            new RefundRequest(10m, "not mine"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactionRefunds_AsParty_ReturnsListWithTotalCount()
    {
        var (transactionId, _, _, _) = await SeedSaleAsync(100m);

        var create = await _client.PostAsJsonAsync($"/api/transactions/{transactionId}/refunds",
            new RefundRequest(25m, "partial"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var response = await _client.GetAsync($"/api/transactions/{transactionId}/refunds",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refunds = await response.Content.ReadFromJsonAsync<List<RefundResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(refunds);
        Assert.Single(refunds);
        Assert.Equal("1", response.Headers.GetValues("X-Total-Count").Single());
    }
}
