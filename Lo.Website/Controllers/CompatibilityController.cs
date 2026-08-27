using Lo.Website.Code.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

public static class CompatibilityController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/v1/compatibility",   Compatibility);
        g.MapGet("/v1/client-version",  ClientVersion);
    }

    private static IResult Compatibility(RevivalConfig cfg)
    {
        return Results.Json(new
        {
            compatibilities = new object[] { },
            version = cfg.Lua.Version
        });
    }

    private static IResult ClientVersion(RevivalConfig cfg)
    {

        return Results.Text(cfg.Lua.Version, "text/plain");
    }
}
