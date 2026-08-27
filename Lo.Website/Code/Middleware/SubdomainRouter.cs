using Lo.Website.Code.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Lo.Website.Code.Middleware;

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

        var md = ep.Metadata.GetMetadata<SubdomainKey>();
        return md?.Key;
    }
}

public class SubdomainKey
{
    public string[] Allowed { get; }
    public string Key => Allowed.FirstOrDefault() ?? "";

    public SubdomainKey(params string[] allowed)
    {
        Allowed = allowed;
    }

    public bool IsAllowed(string? subdomain)
    {
        if (string.IsNullOrEmpty(subdomain)) return false;
        return Allowed.Any(a => string.Equals(a, subdomain, StringComparison.OrdinalIgnoreCase));
    }
}
