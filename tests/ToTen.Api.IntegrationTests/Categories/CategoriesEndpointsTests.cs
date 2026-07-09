using System.Net;
using System.Net.Http.Json;
using ToTen.Api.Features.Categories.GetCategories;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Categories;

public class CategoriesEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var response = await _client.GetAsync("/categories", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<CategoryDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains(result, c => c.Name == "General");
    }

    [Fact]
    public async Task GetCategories_NoAuthRequired_ReturnsOk()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.GetAsync("/categories", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_RespectsPagination()
    {
        var response = await _client.GetAsync("/categories?page=1&pageSize=2", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<CategoryDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());

        Assert.True(response.Headers.TryGetValues("X-Total-Count", out var totalCountValues));
        var totalCount = int.Parse(totalCountValues.Single());
        Assert.Equal(5, totalCount);
    }
}
