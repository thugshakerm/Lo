using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

public static class PlaceController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/universes/validate-place-join", ValidatePlaceJoin);
        g.MapMethods("/universes/{placeId:int}/game-start-info",
            new[] { "GET", "POST" }, GameStartInfo);
    }

    private static IResult ValidatePlaceJoin(HttpContext ctx)
    {
        return Results.Json(new
        {
            status = "OK",
            canJoin = true,
            universeId = (long)0,
            placeId = (long)0,
            authTicket = (string)(ctx.Request.Query["authTicket"].ToString() ?? "")
        });
    }

    private static async Task<IResult> GameStartInfo(HttpContext ctx, long placeId, AppDb db)
    {
        var place = await db.FindPlaceAsync(placeId);
        if (place is null)
        {
            return Results.Json(new { r15Morphing = false, maxPlayers = 20 });
        }
        return Results.Json(new
        {
            r15Morphing = place.R15Morphing,
            maxPlayers = place.MaxPlayers > 0 ? place.MaxPlayers : 20
        });
    }
}
