using System.Net;
using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Testcontainers.Azurite;
using ToTen.Api.Shared.Infrastructure;

namespace ToTen.Api.IntegrationTests.Storage;

/// <summary>
/// Regression coverage for audit finding 1.6: blob storage was public
/// (PublicAccessType.Blob, unsigned URLs, no expiry) and used a container name
/// ("assets") that didn't match the "blobs" container Terraform actually
/// provisions as private. Runs AzureStorageService against a real Azurite
/// container rather than mocking IStorageService/IQRCodeService, since that
/// mocking (used everywhere else in this suite) was exactly why this bug had
/// zero test coverage before.
/// </summary>
public class AzureStorageServiceTests : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite = new AzuriteBuilder().Build();
    private BlobServiceClient _blobServiceClient = null!;
    private AzureStorageService _sut = null!;

    public async ValueTask InitializeAsync()
    {
        await _azurite.StartAsync();
        // Pin an older service API version - the SDK's default (latest) version is
        // newer than what the Azurite emulator image currently understands.
        var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03);
        _blobServiceClient = new BlobServiceClient(_azurite.GetConnectionString(), options);
        _sut = new AzureStorageService(_blobServiceClient, Options.Create(new StorageOptions()));
    }

    public async ValueTask DisposeAsync()
    {
        await _azurite.DisposeAsync();
    }

    [Fact]
    public async Task UploadAsync_CreatesBlobInBlobsContainer_NotAssets()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        await _sut.UploadAsync(content, "test.png", "image/png");

        var blobsContainer = _blobServiceClient.GetBlobContainerClient("blobs");
        Assert.True(await blobsContainer.ExistsAsync(TestContext.Current.CancellationToken));

        var assetsContainer = _blobServiceClient.GetBlobContainerClient("assets");
        Assert.False(await assetsContainer.ExistsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UploadAsync_ContainerIsPrivate_NotPublic()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        await _sut.UploadAsync(content, "test.png", "image/png");

        var blobsContainer = _blobServiceClient.GetBlobContainerClient("blobs");
        var properties = await blobsContainer.GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(
            properties.Value.PublicAccess is null or PublicAccessType.None,
            $"Expected no public access, but was {properties.Value.PublicAccess}");
    }

    [Fact]
    public async Task UploadAsync_ReturnsSignedUrl_ReadableWithSasQuery()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        var url = await _sut.UploadAsync(content, "test.png", "image/png");

        Assert.Contains("sig=", url);

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadAsync_UrlWithoutSasQuery_IsRejected()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        var url = await _sut.UploadAsync(content, "test.png", "image/png");
        var urlWithoutSas = url.Split('?')[0];

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(urlWithoutSas, TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Regression coverage for audit finding 3.4: no content-type/size/extension
    /// validation existed on the upload path. Built into the service layer since no
    /// upload HTTP endpoint exists yet (greenfield) - see StorageOptions.
    /// </summary>
    [Fact]
    public async Task UploadAsync_DisallowedContentType_ThrowsUploadValidationException()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("<script>alert(1)</script>"));

        await Assert.ThrowsAsync<UploadValidationException>(
            () => _sut.UploadAsync(content, "test.html", "text/html"));
    }

    [Fact]
    public async Task UploadAsync_ExtensionDoesNotMatchContentType_ThrowsUploadValidationException()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        await Assert.ThrowsAsync<UploadValidationException>(
            () => _sut.UploadAsync(content, "test.txt", "image/png"));
    }

    [Fact]
    public async Task UploadAsync_ExceedsMaxSize_ThrowsUploadValidationException()
    {
        var oversized = new byte[11 * 1024 * 1024]; // default max is 10 MB
        using var content = new MemoryStream(oversized);

        await Assert.ThrowsAsync<UploadValidationException>(
            () => _sut.UploadAsync(content, "test.png", "image/png"));
    }

    [Fact]
    public async Task UploadAsync_ValidPng_StillSucceeds()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        var url = await _sut.UploadAsync(content, "valid.png", "image/png");

        Assert.NotEmpty(url);
    }
}
