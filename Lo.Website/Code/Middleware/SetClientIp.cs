namespace Lo.Website.Code.Middleware;

/// <summary>
/// Replaces Laravel's SetClientIp middleware. Cloudflare sends
/// CF-Connecting-IP and X-Forwarded-For headers; without this, the
/// rate limiter would throttle every request to 127.0.0.1.
///
/// Note: in production behind Cloudflare Tunnel, the immediate
/// remote is always 127.0.0.1 (cloudflared) and the real client IP
/// is in CF-Connecting-IP. We honor that.
/// </summary>
public class SetClientIp
{
    private readonly RequestDelegate _next;

    public SetClientIp(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var cf = ctx.Request.Headers["CF-Connecting-IP"].ToString();
        if (!string.IsNullOrEmpty(cf))
        {
            ctx.Items["realIp"] = cf;
        }
        else
        {
            var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(xff))
            {
                // XFF is a comma-separated list; first entry is the original client.
                var first = xff.Split(',', 2)[0].Trim();
                ctx.Items["realIp"] = first;
            }
            else
            {
                ctx.Items["realIp"] = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            }
        }
        await _next(ctx);
    }
}

/// <summary>
/// Crude in-memory per-IP rate limit. Replaces Laravel's
/// LimitRequestPerIp middleware.
///
/// Production note: this uses IMemoryCache which is per-instance and
/// resets on restart. For a real revival swap in Redis or a
/// proper WAF. For our load (a few players on a closed test) this
/// is fine.
/// </summary>
public class RateLimit
{
    private readonly RequestDelegate _next;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int count, DateTime windowStart)> _buckets = new();
    private const int MaxPerWindow = 600;       // 600 requests
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public RateLimit(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var ip = ctx.Items["realIp"] as string ?? "0.0.0.0";
        var path = ctx.Request.Path.Value ?? "";
        // Don't rate-limit the FFlags endpoint (client polls it) or healthz
        if (path.StartsWith("/v1/settings") || path == "/healthz")
        {
            await _next(ctx);
            return;
        }

        var now = DateTime.UtcNow;
        var key = $"{ip}|{path}";
        var entry = _buckets.AddOrUpdate(key,
            _ => (1, now),
            (_, prev) =>
            {
                if (now - prev.windowStart > Window) return (1, now);
                return (prev.count + 1, prev.windowStart);
            });

        if (entry.count > MaxPerWindow)
        {
            ctx.Response.StatusCode = 429;
            ctx.Response.Headers["Retry-After"] = "60";
            await ctx.Response.WriteAsync("Too Many Requests");
            return;
        }
        await _next(ctx);
    }
}
