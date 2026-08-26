using Microsoft.AspNetCore.Http;

namespace Lo.Website.Code.Middleware;

/// <summary>
/// Runs AFTER routing. Looks at the selected endpoint's
/// SubdomainKey metadata, compares to the request's actual
/// subdomain (set by SubdomainRouter). If they don't match, return
/// 404 (so the client can't probe endpoints on the wrong subdomain).
///
/// The /healthz route is exempt.
/// </summary>
public class SubdomainGuard
{
    private readonly RequestDelegate _next;

    public SubdomainGuard(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Always allow healthz
        if (ctx.Request.Path.StartsWithSegments("/healthz"))
        {
            await _next(ctx);
            return;
        }

        var ep = ctx.GetEndpoint();
        if (ep is null)
        {
            await _next(ctx);
            return;
        }
        var required = ep.Metadata.GetMetadata<SubdomainKey>()?.Key;
        if (required is null)
        {
            await _next(ctx);
            return;
        }
        var actual = ctx.Items["subdomain"] as string;
        if (string.IsNullOrEmpty(actual))
        {
            // Bare apex hit an endpoint that requires a subdomain. 404.
            ctx.Response.StatusCode = 404;
            return;
        }
        if (!string.Equals(actual, required, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = 404;
            return;
        }
        await _next(ctx);
    }
}
