using Lo.Website.Code.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// applicationcompatibility.&lt;domain&gt; endpoints.
///
/// The 2018M client calls these to learn which versions are mutually
/// compatible. The version-compatibility binary patch zeros out the
/// `versioncompatibility` string in the client, so most of this is
/// stubbed. We still implement the endpoints in case a non-patched
/// client hits them.
///
/// Source: wiki/infrastructure/network/ssl-https.md (version
/// compatibility section).
/// </summary>
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
        // Could load from disk; for the revival we just use the
        // config value (matches Finobe's default).
        return Results.Text(cfg.Lua.Version, "text/plain");
    }
}
