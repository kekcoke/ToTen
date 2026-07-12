using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Marketplace.CounterOffer;

public record CounterOfferRequest([property: Range(0.01, double.MaxValue)] decimal CounterAmount);
