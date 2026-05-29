using ToTen.Api.Features.Marketplace.AcceptOffer;
using ToTen.Api.Features.Marketplace.CreateListing;
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
        app.MapSearchListings();
    }
}
