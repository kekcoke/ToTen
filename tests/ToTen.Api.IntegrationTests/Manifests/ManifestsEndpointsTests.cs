using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.Features.Manifests.AssociateBoxes;
using ToTen.Api.Features.Manifests.CreateManifest;
using ToTen.Api.Features.Manifests.GenerateQR;
using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

namespace ToTen.Api.IntegrationTests.Manifests;

public class ManifestsEndpointsTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid sourceId, Guid destId)> CreateLocationsAsync()
    {
        var src = await _client.PostAsJsonAsync("/api/locations",
            new CreateLocationRequest("Source", null, null, null),
            TestContext.Current.CancellationToken);
        var dest = await _client.PostAsJsonAsync("/api/locations",
            new CreateLocationRequest("Destination", null, null, null),
            TestContext.Current.CancellationToken);
        var srcLoc = await src.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        var destLoc = await dest.Content.ReadFromJsonAsync<LocationResponse>(TestContext.Current.CancellationToken);
        return (srcLoc!.Id, destLoc!.Id);
    }

    [Fact]
    public async Task CreateManifest_ReturnsCreated_WithDraftStatus()
    {
        var (srcId, destId) = await CreateLocationsAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(srcId, destId, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ManifestResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(ManifestStatus.Draft, result.Status);
        Assert.Equal(srcId, result.SourceLocationId);
        Assert.Equal(destId, result.DestinationLocationId);
    }

    [Fact]
    public async Task CreateManifest_NoAuth_Returns401()
    {
        var unauthClient = factory.CreateUnauthenticatedClient();

        var response = await unauthClient.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(Guid.NewGuid(), Guid.NewGuid(), null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenerateQR_OwnedBox_ReturnsOkWithUrl()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var location = new ToTen.Api.Models.Location
        {
            Id = Guid.NewGuid(),
            Name = "QR Test Location",
            OwnerId = factory.DefaultTestUserId.ToString()
        };
        var box = new Box
        {
            Id = Guid.NewGuid(),
            Name = "QR Box",
            LocationId = location.Id,
            OwnerId = factory.DefaultTestUserId.ToString()
        };
        ctx.Locations.Add(location);
        ctx.Boxes.Add(box);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsync(
            $"/api/boxes/{box.Id}/qr",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GenerateQRResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Contains("test.blob.core.windows.net", result.QrCodeUrl);
    }

    [Fact]
    public async Task AssociateBoxes_WithOwnedBoxes_ReturnsNoContent()
    {
        var (srcId, destId) = await CreateLocationsAsync();

        var manifestResponse = await _client.PostAsJsonAsync(
            "/api/manifests",
            new CreateManifestRequest(srcId, destId, null),
            TestContext.Current.CancellationToken);
        var manifest = await manifestResponse.Content.ReadFromJsonAsync<ManifestResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(manifest);

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();
        var box = new Box
        {
            Id = Guid.NewGuid(),
            Name = "Assoc Box",
            LocationId = srcId,
            OwnerId = factory.DefaultTestUserId.ToString()
        };
        ctx.Boxes.Add(box);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync(
            $"/api/manifests/{manifest.Id}/boxes",
            new AssociateBoxesRequest([box.Id]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GenerateQR_UnownedBox_ReturnsForbidden()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var otherUserId = Guid.NewGuid();
        var location = new ToTen.Api.Models.Location
        {
            Id = Guid.NewGuid(),
            Name = "Other QR Location",
            OwnerId = otherUserId.ToString()
        };
        var box = new Box
        {
            Id = Guid.NewGuid(),
            Name = "Other User Box",
            LocationId = location.Id,
            OwnerId = otherUserId.ToString()
        };
        ctx.Locations.Add(location);
        ctx.Boxes.Add(box);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.PostAsync(
            $"/api/boxes/{box.Id}/qr",
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
