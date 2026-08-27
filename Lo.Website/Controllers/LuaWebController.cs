using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

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
