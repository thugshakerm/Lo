using System.Xml.Linq;
using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

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
            "fm"   => 10,
            "fd"   => 13,
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
