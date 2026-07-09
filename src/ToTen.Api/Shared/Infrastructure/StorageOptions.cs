namespace ToTen.Api.Shared.Infrastructure;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public long MaxUploadSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    public string[] AllowedContentTypes { get; set; } = ["image/png", "image/jpeg", "image/webp"];
}
