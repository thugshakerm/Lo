using System.Xml.Linq;
using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// /Game/Tools/InsertAsset.ashx
///
/// Used by the Studio "Insert" tool (free models, free decals, base
/// sets, user sets, collections). The 2018M Studio's InsertService
/// is configured in Gameserver.lua with URLs like:
///
///   InsertService:SetFreeModelUrl(
///     "http://<assetgame>/Game/Tools/InsertAsset.ashx?type=fm&q=%s&pg=%d&rs=%d"
///   )
///
/// The `type` query parameter determines which kind of insert this is:
/// fm = free model, fd = free decal, base = base set, user = user's sets.
/// Returns an XML body with a list of asset IDs.
///
/// Source: wiki/api-docs.md.
/// </summary>
public static class InsertController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/Game/Tools/InsertAsset.ashx", Insert);
    }

    private static async Task<IResult> Insert(HttpContext ctx, AppDb db)
    {
        var type = (ctx.Request.Query["type"].ToString() ?? "fm").ToLowerInvariant();
        var q    = (ctx.Request.Query["q"].ToString() ?? "");
        var pg   = (int)1; int.TryParse(ctx.Request.Query["pg"], out pg);
        var rs   = (int)10; int.TryParse(ctx.Request.Query["rs"], out rs);

        int? assetType = type switch
        {
            "fm"   => 10,  // Model
            "fd"   => 13,  // Decal
            "base" => null,
            "user" => null,
            _      => 10,
        };

        var list = await db.ListAssetsByTypeAsync(assetType, "n", true, pg, rs);

        XNamespace x = "http://www.roblox.com";
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(x + "InsertAssets",
                list.Select(a => new XElement(x + "Asset",
                    new XElement(x + "Id", a.Id),
                    new XElement(x + "Name", a.Name)
                ))
            )
        );
        return Results.Content(xml.ToString(), "application/xml");
    }
}
