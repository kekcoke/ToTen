using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using Rebus.Bus;
using ToTen.Contracts;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.CreateListing;

public static class CreateListingEndpoint
{
    public static void MapCreateListing(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/listings", async (
            CreateListingRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            IBus bus,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var item = await context.InventoryItems.FindAsync(request.InventoryItemId);
            if (item == null) return Results.NotFound("Item not found.");

            // Verify ownership
            if (item.OwnerId != user.Id.ToString()) return Results.Forbid();

            var listing = new Listing
            {
                Id = Guid.NewGuid(),
                InventoryItemId = request.InventoryItemId,
                Price = request.Price,
                ReleaseDate = request.ReleaseDate,
                IsActive = true
            };

            context.Listings.Add(listing);
            await context.SaveChangesAsync();

            // Publish event
            await bus.Publish(new ItemListingEvent(
                item.Id,
                listing.Id,
                listing.Price));

            var response = new ListingResponse(
                listing.Id,
                listing.InventoryItemId,
                listing.Price,
                listing.ReleaseDate,
                listing.IsActive);

            return Results.Created($"/api/listings/{listing.Id}", response);
        })
        .WithName("CreateListing")
        .WithTags("Marketplace")
        .RequireAuthorization();
    }
}
