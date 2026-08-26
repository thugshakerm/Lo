using Lo.Website.Code.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Lo.Website.Code.Middleware;

/// <summary>
/// Looks at the Host header, figures out which subdomain this request
/// is for, and stores it in HttpContext.Items["subdomain"]. Routes
/// that should only match a particular subdomain are guarded by
/// RequireSubdomain() in the route group.
///
/// Anonymous / bare-apex requests (no subdomain) are allowed through
/// to /healthz only.
/// </summary>
public class SubdomainRouter
{
    private readonly RequestDelegate _next;
    private readonly RevivalConfig _cfg;

    public SubdomainRouter(RequestDelegate next, RevivalConfig cfg)
    {
        _next = next;
        _cfg = cfg;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var host = ctx.Request.Host.Host;
        var suffix = _cfg.Domain.ToLowerInvariant();
        string? subdomain = null;
        if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            var prefix = host[..^suffix.Length];
            if (prefix.EndsWith(".")) prefix = prefix[..^1];
            if (!string.IsNullOrEmpty(prefix)) subdomain = prefix.ToLowerInvariant();
        }
        ctx.Items["subdomain"] = subdomain;
        ctx.Items["apex"]      = suffix;
        ctx.Items["allowedSubdomain"] = GetAllowedSubdomain(ctx);
        await _next(ctx);
    }

    private static string? GetAllowedSubdomain(HttpContext ctx)
    {
        var ep = ctx.GetEndpoint();
        if (ep is null) return null;
        // The endpoint metadata carries a "SubdomainKey" string that
        // each controller's Map() method sets. We read it back here.
        var md = ep.Metadata.GetMetadata<SubdomainKey>();
        return md?.Key;
    }
}

/// <summary>
/// Attached to route groups (via .WithMetadata(new SubdomainKey("www"))).
/// The SubdomainRouter middleware uses it to require a particular
/// subdomain on the request's Host header. Mismatches get a 404.
/// </summary>
public class SubdomainKey
{
    public string Key { get; }
    public SubdomainKey(string key) { Key = key; }
}
