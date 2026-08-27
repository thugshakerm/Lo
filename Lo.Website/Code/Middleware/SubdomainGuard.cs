using Microsoft.AspNetCore.Http;

namespace Lo.Website.Code.Middleware;

public class SubdomainGuard
{
    private readonly RequestDelegate _next;

    public SubdomainGuard(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {

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
        var key = ep.Metadata.GetMetadata<SubdomainKey>();
        if (key is null)
        {
            await _next(ctx);
            return;
        }
        var actual = ctx.Items["subdomain"] as string;
        if (!key.IsAllowed(actual))
        {

            ctx.Response.StatusCode = 404;
            return;
        }
        await _next(ctx);
    }
}
