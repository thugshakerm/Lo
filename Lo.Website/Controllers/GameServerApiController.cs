using Lo.Website.Code.Data;
using Lo.Website.Models;
using Lo.Rcc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// Game-server-side glue endpoints. The Lua VM running inside the
/// RCC-spawned child process calls these to register itself,
/// heartbeat, record visits, and shut down cleanly.
///
/// Real Roblox has internal mechanisms for this; revivals expose
/// them as plain HTTP for ease of patching.
/// </summary>
public static class GameServerApiController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/Game/ServerPing", Ping);
        g.MapGet("/Game/KillServer", Kill);
        g.MapMethods("/api/gameserver/register/{jobId}", new[] { "GET", "POST" }, Register);
        g.MapMethods("/api/gameserver/visit/{jobId}",    new[] { "GET", "POST" }, Visit);
    }

    private static async Task<IResult> Ping(HttpContext ctx, RccClient rcc)
    {
        var jobId = ctx.Request.Query["jobId"].ToString();
        if (!string.IsNullOrEmpty(jobId))
        {
            await rcc.RenewLeaseAsync(jobId, 600);
        }
        return Results.Text("pong", "text/plain");
    }

    private static async Task<IResult> Kill(HttpContext ctx, RccClient rcc)
    {
        var jobId = ctx.Request.Query["jobId"].ToString();
        if (!string.IsNullOrEmpty(jobId))
        {
            await rcc.CloseJobAsync(jobId);
        }
        return Results.Text("ok", "text/plain");
    }

    private static async Task<IResult> Register(string jobId, HttpContext ctx, AppDb db)
    {
        long placeId = 0; long.TryParse(ctx.Request.Query["placeId"], out placeId);
        int port = 0;     int.TryParse(ctx.Request.Query["port"], out port);
        int maxPlayers = 0; int.TryParse(ctx.Request.Query["maxPlayers"], out maxPlayers);
        bool priv = bool.TryParse(ctx.Request.Query["isPersonalServer"], out var b) && b;

        var gs = new GameServer
        {
            JobId         = jobId,
            PlaceId       = placeId,
            Port          = port,
            MaxPlayers    = maxPlayers,
            PrivateServer = priv,
            Status        = "running",
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(10),
            LastPingAt    = DateTime.UtcNow,
        };
        await db.UpsertGameServerAsync(gs);
        return Results.Json(new { status = "ok", jobId, placeId = gs.PlaceId, port = gs.Port });
    }

    private static IResult Visit(string jobId, HttpContext ctx)
    {
        var userId = (long)0; long.TryParse(ctx.Request.Query["userId"], out userId);
        // For now just log and ack
        return Results.Json(new { status = "ok", jobId, userId });
    }
}
