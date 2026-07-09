using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ToTen.Api.Shared.RateLimiting;

public static class RateLimitingConfiguration
{
    /// <summary>
    /// Named policy for write/money endpoints and the public unauthenticated search
    /// endpoint (audit finding 2.1) — tighter than the global default limiter.
    /// </summary>
    public const string StrictPolicy = "strict";

    public static IServiceCollection AddToTenRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Defense-in-depth global limiter applied to every request.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(StrictPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // Partitioned per logical endpoint (not just per-client) so one
                    // strict endpoint's budget doesn't get consumed by requests to
                    // another, and route parameter values (e.g. a listing id) don't
                    // fragment a single endpoint's budget into many separate ones.
                    $"{GetPartitionKey(context)}:{context.GetEndpoint()?.DisplayName}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10,
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    // IP-based rather than claims-based: UseRateLimiter runs before the
    // framework's auto-inserted authentication middleware in this pipeline,
    // so HttpContext.User isn't populated yet when the partition is chosen.
    private static string GetPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
