using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.CounterOffer;
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
    public async Task CreateListing_InvalidRequest_ReturnsBadRequest()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(ctx, factory.DefaultTestUserId.ToString());

        var request = new CreateListingRequest(item.Id, -5m, DateOnly.FromDateTime(DateTime.Today));

        var response = await _client.PostAsJsonAsync("/api/listings", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    // Seeds listing/offer rows directly via the DbContext rather than through
    // CreateListing/SubmitOffer — those endpoints are already covered above and
    // are StrictPolicy rate-limited (10/min), a budget these tests would otherwise
    // blow through since they only care about the Reject/Counter/Accept/Get behavior.
    private async Task<(Listing Listing, Guid SellerId)> SeedListingAsync(decimal price)
    {
        var sellerId = factory.DefaultTestUserId;
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var item = await SeedItemAsync(ctx, sellerId.ToString());

        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            Price = price,
            ReleaseDate = DateOnly.FromDateTime(DateTime.Today),
            IsActive = true
        };
        ctx.Listings.Add(listing);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (listing, sellerId);
    }

    private async Task<Offer> SeedOfferAsync(
        Guid listingId, string buyerId, decimal amount,
        OfferStatus status = OfferStatus.Pending, decimal? counterAmount = null)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            BuyerId = buyerId,
            Amount = amount,
            Status = status,
            CounterAmount = counterAmount
        };
        ctx.Offers.Add(offer);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return offer;
    }

    [Fact]
    public async Task RejectOffer_BySeller_ReturnsOkAndRejectsOffer()
    {
        var (listing, _) = await SeedListingAsync(100m);
        var offer = await SeedOfferAsync(listing.Id, Guid.NewGuid().ToString(), 80m);

        var rejectResponse = await _client.PostAsync(
            $"/api/offers/{offer.Id}/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var result = await rejectResponse.Content.ReadFromJsonAsync<OfferResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(OfferStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RejectOffer_ByNonOwner_ReturnsForbidden()
    {
        var (listing, _) = await SeedListingAsync(100m);
        var offer = await SeedOfferAsync(listing.Id, Guid.NewGuid().ToString(), 80m);

        var otherClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());
        var rejectResponse = await otherClient.PostAsync(
            $"/api/offers/{offer.Id}/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, rejectResponse.StatusCode);
    }

    [Fact]
    public async Task RejectOffer_AlreadyRejected_ReturnsBadRequest()
    {
        var (listing, _) = await SeedListingAsync(100m);
        var offer = await SeedOfferAsync(listing.Id, Guid.NewGuid().ToString(), 80m, OfferStatus.Rejected);

        var rejectResponse = await _client.PostAsync(
            $"/api/offers/{offer.Id}/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, rejectResponse.StatusCode);
    }

    [Fact]
    public async Task CounterOffer_BySeller_ReturnsOkWithCounteredStatusAndAmount()
    {
        var (listing, _) = await SeedListingAsync(100m);
        var offer = await SeedOfferAsync(listing.Id, Guid.NewGuid().ToString(), 70m);

        var counterResponse = await _client.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/counter", new CounterOfferRequest(85m), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, counterResponse.StatusCode);
        var result = await counterResponse.Content.ReadFromJsonAsync<OfferResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(OfferStatus.Countered, result.Status);
        Assert.Equal(85m, result.CounterAmount);
    }

    [Fact]
    public async Task AcceptCounterOffer_ByBuyer_TransfersOwnershipAtCounterAmount()
    {
        var (listing, sellerId) = await SeedListingAsync(100m);
        var buyerId = Guid.NewGuid();
        var offer = await SeedOfferAsync(listing.Id, buyerId.ToString(), 70m, OfferStatus.Countered, counterAmount: 85m);

        var buyerClient = factory.CreateAuthenticatedClient(userId: buyerId);
        var acceptResponse = await buyerClient.PostAsync(
            $"/api/offers/{offer.Id}/counter/accept", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var result = await acceptResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(result.TryGetProperty("transactionId", out var txnIdProp));

        using var verifyScope = factory.Services.CreateScope();
        var verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ToTenContext>();
        var transaction = await verifyCtx.Transactions.FindAsync([txnIdProp.GetGuid()], TestContext.Current.CancellationToken);
        Assert.NotNull(transaction);
        Assert.Equal(85m, transaction.Amount);
        Assert.Equal(sellerId.ToString(), transaction.SellerId);
    }

    [Fact]
    public async Task RejectCounterOffer_ByBuyer_ReturnsOkAndRejectsOffer()
    {
        var (listing, _) = await SeedListingAsync(100m);
        var buyerId = Guid.NewGuid();
        var offer = await SeedOfferAsync(listing.Id, buyerId.ToString(), 70m, OfferStatus.Countered, counterAmount: 85m);

        var buyerClient = factory.CreateAuthenticatedClient(userId: buyerId);
        var rejectResponse = await buyerClient.PostAsync(
            $"/api/offers/{offer.Id}/counter/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var result = await rejectResponse.Content.ReadFromJsonAsync<OfferResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(OfferStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task AcceptOffer_WithOtherPendingOffers_AutoRejectsSiblingOffers()
    {
        var (listing, _) = await SeedListingAsync(100m);
        var acceptedOffer = await SeedOfferAsync(listing.Id, Guid.NewGuid().ToString(), 95m);
        var otherOffer = await SeedOfferAsync(listing.Id, Guid.NewGuid().ToString(), 80m);

        var acceptResponse = await _client.PostAsync(
            $"/api/offers/{acceptedOffer.Id}/accept", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ToTenContext>();
        var updatedOtherOffer = await verifyCtx.Offers.FindAsync([otherOffer.Id], TestContext.Current.CancellationToken);
        Assert.Equal(OfferStatus.Rejected, updatedOtherOffer?.Status);
    }

    [Fact]
    public async Task GetListingOffers_BySeller_ReturnsOffers()
    {
        var (listing, _) = await SeedListingAsync(100m);
        await SeedOfferAsync(listing.Id, Guid.NewGuid().ToString(), 80m);

        var response = await _client.GetAsync($"/api/listings/{listing.Id}/offers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var offers = await response.Content.ReadFromJsonAsync<List<OfferResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(offers);
        Assert.Single(offers);
        Assert.Equal("1", response.Headers.GetValues("X-Total-Count").First());
    }

    [Fact]
    public async Task GetListingOffers_ByNonOwner_ReturnsForbidden()
    {
        var (listing, _) = await SeedListingAsync(100m);
        var otherClient = factory.CreateAuthenticatedClient(userId: Guid.NewGuid());

        var response = await otherClient.GetAsync($"/api/listings/{listing.Id}/offers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
