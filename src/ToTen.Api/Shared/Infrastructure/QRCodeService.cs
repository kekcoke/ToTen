using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace ToTen.Api.Shared.Infrastructure;

public class QRCodeService : IQRCodeService
{
    private readonly IStorageService _storageService;

    public QRCodeService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<string> GenerateAndSaveQRCodeAsync(string data, string fileName)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);

        using var ms = new MemoryStream(qrCodeBytes);
        return await _storageService.UploadAsync(ms, $"{fileName}.png", "image/png");
    }
}
