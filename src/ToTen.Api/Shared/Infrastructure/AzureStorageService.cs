using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace ToTen.Api.Shared.Infrastructure;

public class AzureStorageService(BlobServiceClient blobServiceClient, IOptions<StorageOptions> options) : IStorageService
{
    private readonly StorageOptions _options = options.Value;
    private const string ContainerName = "blobs";

    // Maps each allowed content type to the file extensions it may legitimately carry,
    // so a caller can't smuggle e.g. a .html file in under "image/png".
    private static readonly Dictionary<string, string[]> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = [".png"],
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/webp"] = [".webp"]
    };

    // QR codes are printed on physical box/location labels and must stay scannable
    // for the life of the label, so reads use a long-lived signed URL rather than
    // a short-lived one - the fix is removing anonymous public access, not making
    // every existing label expire in minutes.
    public static readonly TimeSpan ReadUrlValidity = TimeSpan.FromDays(365);

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        ValidateUpload(content, fileName, contentType);

        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = containerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return GetReadUrl(blobClient);
    }

    private void ValidateUpload(Stream content, string fileName, string contentType)
    {
        if (!_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            || !ContentTypeExtensions.TryGetValue(contentType, out var allowedExtensions))
        {
            throw new UploadValidationException(
                $"Content type '{contentType}' is not allowed. Allowed types: {string.Join(", ", _options.AllowedContentTypes)}.");
        }

        var extension = Path.GetExtension(fileName);
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new UploadValidationException(
                $"File extension '{extension}' does not match content type '{contentType}'. Expected one of: {string.Join(", ", allowedExtensions)}.");
        }

        if (content.CanSeek && content.Length > _options.MaxUploadSizeBytes)
        {
            throw new UploadValidationException(
                $"File size {content.Length} bytes exceeds the maximum allowed size of {_options.MaxUploadSizeBytes} bytes.");
        }
    }

    public async Task DeleteAsync(string fileUrl)
    {
        var uri = new Uri(fileUrl);
        var blobName = Uri.UnescapeDataString(uri.Segments.Last());
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
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
