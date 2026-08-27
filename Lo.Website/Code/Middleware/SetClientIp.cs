namespace Lo.Website.Code.Middleware;

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

public class RateLimit
{
    private readonly RequestDelegate _next;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int count, DateTime windowStart)> _buckets = new();
    private const int MaxPerWindow = 600;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public RateLimit(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var ip = ctx.Items["realIp"] as string ?? "0.0.0.0";
        var path = ctx.Request.Path.Value ?? "";

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
