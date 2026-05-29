using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ToTen.Api.Data;
using ToTen.Api.Models;

namespace ToTen.Api.Features.Marketplace.Search;

public static class SearchListingsEndpoint
{
    public static void MapSearchListings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/listings/search", async (
            [AsParameters] SearchListingsRequest request,
            ToTenContext context) =>
        {
            var query = context.Listings
                .Include(l => l.InventoryItem)
                .ThenInclude(i => i!.Location)
                .Include(l => l.InventoryItem)
                .ThenInclude(i => i!.Category)
                .Where(l => l.IsActive)
                .AsQueryable();

            // 1. Text Search
            if (!string.IsNullOrWhiteSpace(request.Text))
            {
                query = query.Where(l => l.InventoryItem!.Name.Contains(request.Text) || 
                                         l.InventoryItem.Description.Contains(request.Text));
            }

            // 2. Facets
            if (request.CategoryId.HasValue)
            {
                query = query.Where(l => l.InventoryItem!.CategoryId == request.CategoryId.Value);
            }

            if (request.MinPrice.HasValue)
            {
                query = query.Where(l => l.Price >= request.MinPrice.Value);
            }

            if (request.MaxPrice.HasValue)
            {
                query = query.Where(l => l.Price <= request.MaxPrice.Value);
            }

            if (request.OrganizationId.HasValue)
            {
                query = query.Where(l => l.InventoryItem!.OrganizationId == request.OrganizationId.Value);
            }

            // 3. Geolocation (Radius Search)
            Point? userLocation = null;
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                userLocation = new Point(request.Longitude.Value, request.Latitude.Value) { SRID = 4326 };
                
                if (request.RadiusInKm.HasValue)
                {
                    // Distance in meters for ST_DWithin if using geography, 
                    // or unit-less for ST_Distance if using geometry.
                    // EF Core NTS .Distance() works with the coordinate units.
                    // For SRID 4326, we typically use Distance on a projected set or use specific NTS methods.
                    // Here we'll use Distance for filtering and sorting.
                    double radiusInDegrees = request.RadiusInKm.Value / 111.32; // Rough approximation
                    query = query.Where(l => l.InventoryItem!.Location != null && 
                                             l.InventoryItem.Location.Coordinates != null &&
                                             l.InventoryItem.Location.Coordinates.Distance(userLocation) <= radiusInDegrees);
                }
            }

            // 4. Sorting
            if (request.SortBy == "Price")
            {
                query = query.OrderBy(l => l.Price);
            }
            else if (request.SortBy == "Distance" && userLocation != null)
            {
                query = query.OrderBy(l => l.InventoryItem!.Location!.Coordinates!.Distance(userLocation));
            }
            else
            {
                query = query.OrderByDescending(l => l.ReleaseDate);
            }

            var totalCount = await query.CountAsync();
            
            var listings = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(l => new SearchListingResponse(
                    l.Id,
                    l.InventoryItem!.Name,
                    l.InventoryItem.Category!.Name,
                    l.Price,
                    userLocation != null && l.InventoryItem.Location != null && l.InventoryItem.Location.Coordinates != null
                        ? l.InventoryItem.Location.Coordinates.Distance(userLocation) * 111.32 // Convert back to KM
                        : null,
                    l.InventoryItem.OrganizationId
                ))
                .ToListAsync();

            return Results.Ok(new SearchListingsResponse(listings, totalCount));
        })
        .WithName("SearchListings")
        .WithTags("Marketplace");
    }
}
