using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Shared.Infrastructure;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Manifests.GenerateQR;

public static class GenerateQREndpoint
{
    public static void MapGenerateQR(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/boxes/{boxId:guid}/qr", async (
            Guid boxId,
            ToTenContext context,
            IQRCodeService qrCodeService,
            IIdentityManager identityManager,
            System.Security.Claims.ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var box = await context.Boxes.FindAsync(boxId);
            if (box == null) return Results.NotFound();

            // Verify ownership
            if (box.OwnerId != user.Id.ToString()) return Results.Forbid();

            // Generate QR Data (Internal URL or ID)
            var qrData = $"toten://boxes/{box.Id}";
            var fileName = $"qr-box-{box.Id}";

            var qrUrl = await qrCodeService.GenerateAndSaveQRCodeAsync(qrData, fileName);

            return Results.Ok(new GenerateQRResponse(qrUrl));
        })
        .WithName("GenerateBoxQR")
        .WithTags("Manifests")
        .RequireAuthorization();
    }
}
