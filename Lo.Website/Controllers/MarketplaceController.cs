using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// api.&lt;domain&gt; — Marketplace endpoints
///
/// - /marketplace/productinfo     (set by SetProductInfoUrl in Gameserver.lua)
/// - /marketplace/productDetails  (set by SetDevProductInfoUrl)
/// - /ownership/hasasset          (set by SetPlayerOwnsAssetUrl)
///
/// Source: wiki/api-docs.md.
/// </summary>
public static class MarketplaceController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapMethods("/marketplace/productinfo",    new[] { "GET", "POST" }, ProductInfo);
        g.MapMethods("/marketplace/productDetails", new[] { "GET", "POST" }, ProductDetails);
        g.MapMethods("/ownership/hasasset",         new[] { "GET", "POST" }, HasAsset);
    }

    private static async Task<IResult> ProductInfo(HttpContext ctx, AppDb db)
    {
        var assetId = (long)0; long.TryParse(ctx.Request.Query["assetId"], out assetId);
        var asset = await db.FindAssetAsync(assetId);
        if (asset is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        return Results.Json(new
        {
            AssetId         = asset.Id,
            Name            = asset.Name,
            Description     = asset.Description ?? "",
            Creator         = new { Id = asset.CreatorId, Name = "Unknown" },
            PriceInRobux    = asset.Price,
            IsForSale       = asset.IsForSale,
            IsLimited       = false,
            IsLimitedUnique = false,
            Sales           = 0
        });
    }

    private static IResult ProductDetails(HttpContext ctx)
    {
        // Dev products: not implemented in the revival yet.
        return Results.Json(new { error = "dev products not supported" }, statusCode: 501);
    }

    private static IResult HasAsset(HttpContext ctx, AppDb db)
    {
        var userId  = (long)0; long.TryParse(ctx.Request.Query["userId"],  out userId);
        var assetId = (long)0; long.TryParse(ctx.Request.Query["assetId"], out assetId);
        if (userId == 0 || assetId == 0) return Results.Json(new { success = false });
        // TODO: real check via asset_ownership
        return Results.Json(new { success = false });
    }
}
