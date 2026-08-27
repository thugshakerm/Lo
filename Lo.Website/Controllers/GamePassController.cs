using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

public static class GamePassController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapMethods("/Game/GamePass/GamePassHandler.ashx",
            new[] { "GET", "POST" }, Handler);
    }

    private static async Task<IResult> Handler(HttpContext ctx, AppDb db)
    {
        var action = (ctx.Request.Query["Action"].ToString() ?? "HasPass").Trim();
        return action switch
        {
            "HasPass" => Results.Text(await HasPass(ctx, db), "text/plain"),
            _         => Results.Text("False", "text/plain"),
        };
    }

    private static async Task<string> HasPass(HttpContext ctx, AppDb db)
    {
        var userId = (long)0; long.TryParse(ctx.Request.Query["UserID"], out userId);
        var passId = (long)0; long.TryParse(ctx.Request.Query["PassID"], out passId);
        if (userId == 0 || passId == 0) return "False";
        return await db.UserOwnsGamePassAsync(userId, passId) ? "True" : "False";
    }
}
