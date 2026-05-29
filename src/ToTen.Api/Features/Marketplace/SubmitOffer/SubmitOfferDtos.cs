using ToTen.Api.Models;

namespace ToTen.Api.Features.Marketplace.SubmitOffer;

public record SubmitOfferRequest(decimal Amount);

public record OfferResponse(
    Guid Id,
    Guid ListingId,
    decimal Amount,
    OfferStatus Status);
