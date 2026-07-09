using System.ComponentModel.DataAnnotations;
using ToTen.Api.Models;

namespace ToTen.Api.Features.Marketplace.SubmitOffer;

public record SubmitOfferRequest([property: Range(0.01, double.MaxValue)] decimal Amount);

public record OfferResponse(
    Guid Id,
    Guid ListingId,
    decimal Amount,
    OfferStatus Status);
