using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// /Game/LuaWebService/HandleSocialRequest.ashx
///
/// Called by Lua scripts inside the game server via
/// SocialService methods. Each method is a separate URL parameter:
///   - IsFriendsWith
///   - IsBestFriendsWith
///   - IsInGroup
///   - GetGroupRank
///   - GetGroupRole
///
/// Returns "True" or "False" (or a numeric value for the rank/role
/// methods) as plain text.
///
/// Source: wiki/api-docs.md.
/// </summary>
public static class LuaWebController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapMethods("/Game/LuaWebService/HandleSocialRequest.ashx",
            new[] { "GET", "POST" }, Social);
    }

    private static IResult Social(HttpContext ctx)
    {
        var method   = (ctx.Request.Query["method"].ToString() ?? "").Trim();
        var playerId = (long)0; long.TryParse(ctx.Request.Query["playerid"], out playerId);
        var userId   = (long)0; long.TryParse(ctx.Request.Query["userid"],   out userId);
        var groupId  = (long)0; long.TryParse(ctx.Request.Query["groupid"],  out groupId);

        var result = method switch
        {
            "IsFriendsWith"     => "False",
            "IsBestFriendsWith" => "False",
            "IsInGroup"         => "False",
            "GetGroupRank"      => "0",
            "GetGroupRole"      => "",
            _                   => "False",
        };
        return Results.Text(result, "text/plain");
    }
}
