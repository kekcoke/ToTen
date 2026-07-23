using ToTen.Api.Features.Marketplace.AcceptOffer;
using ToTen.Api.Features.Marketplace.CounterOffer;
using ToTen.Api.Features.Marketplace.CreateListing;
using ToTen.Api.Features.Marketplace.GetListingOffers;
using ToTen.Api.Features.Marketplace.GetTransactions;
using ToTen.Api.Features.Marketplace.RejectOffer;
using ToTen.Api.Features.Marketplace.RespondToCounterOffer;
using ToTen.Api.Features.Marketplace.Search;
using ToTen.Api.Features.Marketplace.SubmitOffer;

namespace ToTen.Api.Features.Marketplace;

public static class MarketplaceEndpoints
{
    public static void MapMarketplaceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateListing();
        app.MapSubmitOffer();
        app.MapAcceptOffer();
        app.MapRejectOffer();
        app.MapCounterOffer();
        app.MapRespondToCounterOffer();
        app.MapGetListingOffers();
        app.MapGetTransactions();
        app.MapSearchListings();
    }
}
