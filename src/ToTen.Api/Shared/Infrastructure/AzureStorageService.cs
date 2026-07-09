using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace ToTen.Api.Shared.Infrastructure;

public class AzureStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private const string ContainerName = "blobs";

    // QR codes are printed on physical box/location labels and must stay scannable
    // for the life of the label, so reads use a long-lived signed URL rather than
    // a short-lived one - the fix is removing anonymous public access, not making
    // every existing label expire in minutes.
    public static readonly TimeSpan ReadUrlValidity = TimeSpan.FromDays(365);

    public AzureStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = containerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return GetReadUrl(blobClient);
    }

    public async Task DeleteAsync(string fileUrl)
    {
        var uri = new Uri(fileUrl);
        var blobName = Uri.UnescapeDataString(uri.Segments.Last());
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }

    private static string GetReadUrl(BlobClient blobClient)
    {
        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                $"Cannot generate a SAS URI for blob '{blobClient.Name}' - the BlobServiceClient " +
                "must be authenticated with a shared key (connection string), not Azure AD/managed identity.");
        }

        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(ReadUrlValidity));
        return sasUri.ToString();
    }
}
