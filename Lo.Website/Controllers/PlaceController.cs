using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// api.&lt;domain&gt; — Place-related endpoints.
///
/// - GET /universes/validate-place-join   (auth patch target)
/// - GET /universes/{placeId}/game-start-info
/// </summary>
public static class PlaceController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/universes/validate-place-join", ValidatePlaceJoin);
        g.MapMethods("/universes/{placeId:int}/game-start-info",
            new[] { "GET", "POST" }, GameStartInfo);
    }

    /// <summary>
    /// GET /universes/validate-place-join
    /// The 2018L+ "auth patch" target. With the patch applied, the
    /// binary JMPs over the call and this endpoint is never actually
    /// hit. We still implement it as a permissive success response.
    /// </summary>
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

    /// <summary>
    /// GET /universes/{placeId}/game-start-info
    /// Per-place configuration: r15Morphing, maxPlayers, privateServerOwnerId.
    /// </summary>
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
