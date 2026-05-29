namespace ToTen.Api.Shared.Infrastructure;

public interface IStorageService
{
    Task<string> UploadAsync(Stream content, string fileName, string contentType);
    Task DeleteAsync(string fileUrl);
}
