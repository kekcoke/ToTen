using Microsoft.Net.Http.Headers;

namespace ToTen.Api.Shared.Cors;

public static class CorsExtensions
{
    public static IHostApplicationBuilder AddToTenCors(this IHostApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(
                policy =>
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        policy.AllowAnyOrigin();
                    }
                    else
                    {
                        var originsString = builder.Configuration["AllowedOrigins"] ?? string.Empty;
                        var allowedOrigins = originsString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        policy.WithOrigins(allowedOrigins);
                    }

                    policy.WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType, "X-CSRF-Token")
                          .AllowAnyMethod();
                });
        });

        return builder;
    }
}