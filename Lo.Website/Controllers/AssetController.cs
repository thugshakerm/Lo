using Lo.Website.Code.Data;
using Lo.Website.Code.Sign;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// /Asset/* — content delivery
///
/// The 2018M client fetches every Lua script, image, mesh, audio,
/// animation, and place from /Asset/?id={id}. This is the most-called
/// endpoint in the whole revival.
///
/// Behavior matrix (mirroring Finobe's rbxAPIs::asset):
///
///   1. If assetId is in storage\rbx\files\2018CoreGui\      -> signed Lua
///   2. If assetId is in our DB and approved                 -> raw bytes
///   3. If assetId is in our DB and moderated/restricted     -> 200 with empty body
///   4. If assetId is audio (AssetType 3)                    -> supports HTTP Range
///   5. Otherwise                                            -> 200 with empty body
///
/// Source: wiki/api-docs.md.
/// </summary>
public static class AssetController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/Asset",           Fetch);
        g.MapGet("/Asset/",          Fetch);
        g.MapGet("/Asset/{id:long}", FetchById);
    }

    private static Task<IResult> Fetch(HttpContext ctx, AppDb db, SecurityNotary notary) =>
        ServeAssetAsync(ctx, db, notary);

    private static Task<IResult> FetchById(long id, HttpContext ctx, AppDb db, SecurityNotary notary) =>
        ServeAssetAsync(ctx, db, notary, id);

    private static async Task<IResult> ServeAssetAsync(HttpContext ctx, AppDb db, SecurityNotary notary, long? idHint = null)
    {
        long id = idHint ?? 0;
        if (id == 0)
        {
            long.TryParse(ctx.Request.Query["id"].ToString(), out id);
            if (id == 0) long.TryParse(ctx.Request.Query["ID"].ToString(), out id);
        }
        if (id == 0) return Results.Text("", "application/octet-stream");

        // 1. CoreGui-style wrapped Lua (highest priority)
        var coreGuiPath = Path.Combine(@"C:\lo\storage\rbx\files\2018CoreGui", $"{id}.lua");
        if (File.Exists(coreGuiPath))
        {
            var body = File.ReadAllText(coreGuiPath);
            var wrapped = $"%{id}%\r\n{body}";
            var signed = notary.SignCoreGuiBody(wrapped, FormatVersion.V2);
            return Results.Content(signed, "text/plain");
        }

        // 2. DB lookup
        var asset = await db.FindAssetAsync(id);
        if (asset is null) return Results.Text("", "application/octet-stream");

        if (!asset.IsApproved) return Results.Text("", "application/octet-stream");

        // 3. Resolve the file
        if (string.IsNullOrEmpty(asset.StoragePath) || !File.Exists(asset.StoragePath))
        {
            // Fall back to the conventional storage path
            var fallback = Path.Combine(@"C:\lo\storage\rbx\files\assets", asset.Id.ToString());
            if (!File.Exists(fallback)) return Results.Text("", "application/octet-stream");
            return BytesOrRange(ctx, File.ReadAllBytes(fallback), asset.MimeType ?? "application/octet-stream");
        }
        return BytesOrRange(ctx, File.ReadAllBytes(asset.StoragePath), asset.MimeType ?? "application/octet-stream");
    }

    /// <summary>
    /// Honor HTTP Range requests so audio can seek. The 2018M client
    /// sends Range: bytes=A-B for audio assets.
    /// </summary>
    private static IResult BytesOrRange(HttpContext ctx, byte[] data, string contentType)
    {
        var range = ctx.Request.Headers["Range"].ToString();
        if (string.IsNullOrEmpty(range))
        {
            return Results.Bytes(data, contentType);
        }
        // Parse "bytes=A-B"
        if (!range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Bytes(data, contentType);
        }
        var spec = range["bytes=".Length..];
        var dash = spec.IndexOf('-');
        if (dash < 0) return Results.Bytes(data, contentType);
        var startStr = spec[..dash];
        var endStr = spec[(dash + 1)..];
        long start = string.IsNullOrEmpty(startStr) ? 0 : long.Parse(startStr);
        long end = string.IsNullOrEmpty(endStr) ? data.Length - 1 : Math.Min(long.Parse(endStr), data.Length - 1);
        if (start > end || start >= data.Length)
        {
            ctx.Response.StatusCode = 416;
            ctx.Response.Headers["Content-Range"] = $"bytes */{data.Length}";
            return Results.Bytes(Array.Empty<byte>(), contentType);
        }

        var slice = new byte[end - start + 1];
        Array.Copy(data, start, slice, 0, slice.Length);

        ctx.Response.StatusCode = 206;
        ctx.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{data.Length}";
        ctx.Response.Headers["Accept-Ranges"]  = "bytes";
        return Results.Bytes(slice, contentType);
    }
}
