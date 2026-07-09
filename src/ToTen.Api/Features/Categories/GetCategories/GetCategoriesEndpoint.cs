using ToTen.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ToTen.Api.Features.Categories.GetCategories;

public static class GetCategoriesEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetCategories(this IEndpointRouteBuilder app)
    {
        // GET /categories
        app.MapGet("/", async (
            HttpContext httpContext,
            ToTenContext dbContext,
            int page = 1,
            int pageSize = 20) =>
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var totalCount = await dbContext.Categories.CountAsync();

            var categories = await dbContext.Categories
                .OrderBy(category => category.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(category => new CategoryDto(category.Id, category.Name))
                .AsNoTracking()
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return categories;
        })
        .Produces<List<CategoryDto>>();
    }
}
