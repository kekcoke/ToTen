using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ToTen.Api.Shared.Authorization;

/// <summary>
/// Requires a valid X-CSRF-Token header on mutating requests authenticated via the Cookies
/// scheme (mobile's bearer callers are naturally immune — no ambient credential). Runs as an
/// endpoint filter (not raw middleware) so it executes after authentication/authorization have
/// already populated HttpContext.User; applied globally via the route group in Program.cs
/// rather than per-endpoint, so no existing Features/*/*.cs endpoint registration needs to change.
/// </summary>
public class CsrfValidationFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete,
    };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var isCookieAuthenticated = httpContext.User.Identity?.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme;

        if (isCookieAuthenticated && MutatingMethods.Contains(httpContext.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(httpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing or invalid CSRF token.");
            }
        }

        return await next(context);
    }
}
