using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

public static class BadgesController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapMethods("/Game/Badges/BadgeHandler.ashx",
            new[] { "GET", "POST" }, Handler);
    }

    private static IResult Handler(HttpContext ctx)
    {
        var action = (ctx.Request.Query["Action"].ToString() ?? "HasBadge").Trim();
        return action switch
        {
            "HasBadge"        => Results.Text(HasBadge(ctx),        "text/plain"),
            "AwardBadge"      => Results.Text(AwardBadge(ctx),      "text/plain"),
            "IsBadgeDisabled" => Results.Text(IsBadgeDisabled(ctx), "text/plain"),
            _                 => Results.Text("False",              "text/plain"),
        };
    }

    private static string HasBadge(HttpContext ctx)
    {
        var userId  = (long)0; long.TryParse(ctx.Request.Query["UserID"],  out userId);
        var badgeId = (long)0; long.TryParse(ctx.Request.Query["BadgeID"], out badgeId);
        if (userId == 0 || badgeId == 0) return "False";

        return "False";
    }

    private static string AwardBadge(HttpContext ctx)
    {
        var userId  = (long)0; long.TryParse(ctx.Request.Query["UserID"],  out userId);
        var badgeId = (long)0; long.TryParse(ctx.Request.Query["BadgeID"], out badgeId);
        if (userId == 0 || badgeId == 0) return "False";

        return "False";
    }

    private static string IsBadgeDisabled(HttpContext ctx)
    {
        var badgeId = (long)0; long.TryParse(ctx.Request.Query["BadgeID"], out badgeId);
        if (badgeId == 0) return "False";
        return "False";
    }
}
