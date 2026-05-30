using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.CreateListing;
using ToTen.Api.Features.Marketplace.Search;
using ToTen.Api.Features.Marketplace.SubmitOffer;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Marketplace;

public class MarketplaceEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<InventoryItem> SeedItemAsync(ToTenContext ctx, string ownerId)
    {
        var categoryId = factory.GetSeedCategoryId();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Listed Item",
            Description = "For marketplace test",
            CategoryId = categoryId,
            OwnerId = ownerId,
            LastUpdatedBy = "test"
        };
        ctx.InventoryItems.Add(item);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task CreateListing_WithOwnedItem_ReturnsCreated()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(ctx, factory.DefaultTestUserId.ToString());

        var request = new CreateListingRequest(item.Id, 99.99m, DateOnly.FromDateTime(DateTime.Today.AddDays(7)));

        var response = await _client.PostAsJsonAsync("/api/listings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ListingResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.True(result.IsActive);
        Assert.Equal(99.99m, result.Price);
    }

    [Fact]
    public async Task CreateListing_UnownedItem_ReturnsForbidden()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(ctx, Guid.NewGuid().ToString());

        var request = new CreateListingRequest(item.Id, 50m, DateOnly.FromDateTime(DateTime.Today));

        var response = await _client.PostAsJsonAsync("/api/listings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SearchListings_NoAuth_ReturnsOk()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync("/api/listings/search", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SearchListingsResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchListings_PriceFilter_ReturnsOnlyMatchingListings()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var lowItem = await SeedItemAsync(ctx, factory.DefaultTestUserId.ToString());
        var highItem = await SeedItemAsync(ctx, factory.DefaultTestUserId.ToString());

        await _client.PostAsJsonAsync("/api/listings",
            new CreateListingRequest(lowItem.Id, 10m, DateOnly.FromDateTime(DateTime.Today)),
            TestContext.Current.CancellationToken);
        await _client.PostAsJsonAsync("/api/listings",
            new CreateListingRequest(highItem.Id, 500m, DateOnly.FromDateTime(DateTime.Today)),
            TestContext.Current.CancellationToken);

        var response = await _client.GetAsync("/api/listings/search?minPrice=50&maxPrice=200", TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<SearchListingsResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.All(result.Listings, l => Assert.InRange(l.Price, 50m, 200m));
    }

    [Fact]
    public async Task SubmitOffer_ReturnsCreated_WithPendingStatus()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(ctx, factory.DefaultTestUserId.ToString());

        var listingResponse = await _client.PostAsJsonAsync("/api/listings",
            new CreateListingRequest(item.Id, 75m, DateOnly.FromDateTime(DateTime.Today)),
            TestContext.Current.CancellationToken);
        var listing = await listingResponse.Content.ReadFromJsonAsync<ListingResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(listing);

        var buyerClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());
        var response = await buyerClient.PostAsJsonAsync(
            $"/api/listings/{listing.Id}/offers",
            new SubmitOfferRequest(70m),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var offer = await response.Content.ReadFromJsonAsync<OfferResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(offer);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Equal(70m, offer.Amount);
    }

    [Fact]
    public async Task AcceptOffer_BySeller_ReturnsOkAndTransfersOwnership()
    {
        var sellerId = factory.DefaultTestUserId;
        var buyerId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(ctx, sellerId.ToString());

        var listingResponse = await _client.PostAsJsonAsync("/api/listings",
            new CreateListingRequest(item.Id, 100m, DateOnly.FromDateTime(DateTime.Today)),
            TestContext.Current.CancellationToken);
        var listing = await listingResponse.Content.ReadFromJsonAsync<ListingResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(listing);

        var buyerClient = factory.CreateAuthenticatedClient(userId: buyerId);
        var offerResponse = await buyerClient.PostAsJsonAsync(
            $"/api/listings/{listing.Id}/offers",
            new SubmitOfferRequest(95m),
            TestContext.Current.CancellationToken);
        var offer = await offerResponse.Content.ReadFromJsonAsync<OfferResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(offer);

        var acceptResponse = await _client.PostAsync(
            $"/api/offers/{offer.Id}/accept",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var result = await acceptResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(result.TryGetProperty("transactionId", out _));

        using var verifyScope = factory.Services.CreateScope();
        var verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ToTenContext>();
        var updatedItem = await verifyCtx.InventoryItems.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.Equal(buyerId.ToString(), updatedItem?.OwnerId);
    }
}
