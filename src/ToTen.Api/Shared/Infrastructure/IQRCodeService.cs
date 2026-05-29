namespace ToTen.Api.Shared.Infrastructure;

public interface IQRCodeService
{
    Task<string> GenerateAndSaveQRCodeAsync(string data, string fileName);
}
