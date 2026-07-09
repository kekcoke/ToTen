using System.Net;
using System.Net.Http.Json;
using ToTen.Api.Features.Users;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Users;

public class UsersEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    [Fact]
    public async Task GetUsers_WithAdminRole_ReturnsOk()
    {
        var client = factory.CreateAuthenticatedClient(roles: ["admin"]);

        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserResponse[]>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetUsers_WithUserRole_ReturnsForbidden()
    {
        var client = factory.CreateAuthenticatedClient(roles: ["user"]);

        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRoles_WithAdminRole_ReturnsNoContent()
    {
        var client = factory.CreateAuthenticatedClient(roles: ["admin"]);
        var request = new UpdateUserRolesRequest(["user"]);

        var response = await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/roles", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRoles_EmptyRoles_ReturnsBadRequest()
    {
        var client = factory.CreateAuthenticatedClient(roles: ["admin"]);
        var request = new UpdateUserRolesRequest([]);

        var response = await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/roles", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
