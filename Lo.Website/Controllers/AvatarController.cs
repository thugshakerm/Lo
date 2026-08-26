using System.Text.Json;
using System.Xml.Linq;
using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// /Asset/CharacterFetch.ashx and /Asset/BodyColors.ashx
///
/// The 2018M client fetches a player's character appearance from
/// CharacterFetch.ashx (an XML document describing the equipped
/// items) and the body color palette from BodyColors.ashx.
///
/// These are set in Gameserver.lua via:
///   player.CharacterAppearance = "http://<assetgame>/Asset/CharacterFetch.ashx?userId=1&amp;placeId=0"
///
/// Source: wiki/api-docs.md, Finobe rbxAPIs::characterFetch.
/// </summary>
public static class AvatarController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/Asset/CharacterFetch.ashx", CharacterFetch);
        g.MapGet("/Asset/BodyColors.ashx",     BodyColors);
    }

    private static async Task<IResult> CharacterFetch(HttpContext ctx, AppDb db)
    {
        var userId  = (long)0; long.TryParse(ctx.Request.Query["userId"],  out userId);
        var placeId = (long)0; long.TryParse(ctx.Request.Query["placeId"], out placeId);
        if (userId == 0) long.TryParse(ctx.Request.Query["UserID"], out userId);
        if (placeId == 0) long.TryParse(ctx.Request.Query["PlaceID"], out placeId);

        var user = userId == 0 ? null : await db.FindUserAsync(userId);
        if (user is null) return Results.Content(DefaultAppearance(userId), "application/xml");

        var avatar = user.Avatar;
        var colors = TryGetDict(avatar, "body_colors") ?? DefaultBodyColorsDict();
        var head   = (string?)TryGetValue(avatar, "head")  ?? "";
        var face   = (string?)TryGetValue(avatar, "face")  ?? "";
        var hat1   = (string?)TryGetValue(avatar, "hat1")  ?? "";
        var hat2   = (string?)TryGetValue(avatar, "hat2")  ?? "";
        var hat3   = (string?)TryGetValue(avatar, "hat3")  ?? "";
        var shirt  = (string?)TryGetValue(avatar, "shirt") ?? "";
        var pants  = (string?)TryGetValue(avatar, "pants") ?? "";

        XNamespace x = "http://www.roblox.com";
        XNamespace xmime = "http://www.w3.org/2005/05/xmlmime";
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("roblox", new XAttribute(XNamespace.Xmlns + "xmime", xmime.NamespaceName),
                new XElement("Item", new XAttribute("class", "CharacterAppearance"),
                    new XElement("Properties",
                        new XElement("int",   new XAttribute("name", "BodyColor"),     ColorValue(colors, "body")),
                        new XElement("int",   new XAttribute("name", "LeftArmColor"),  ColorValue(colors, "left_arm")),
                        new XElement("int",   new XAttribute("name", "RightArmColor"), ColorValue(colors, "right_arm")),
                        new XElement("int",   new XAttribute("name", "LeftLegColor"),  ColorValue(colors, "left_leg")),
                        new XElement("int",   new XAttribute("name", "RightLegColor"), ColorValue(colors, "right_leg")),
                        new XElement("int",   new XAttribute("name", "HeadColor"),     ColorValue(colors, "head")),
                        new XElement("string", new XAttribute("name", "Head"),  head),
                        new XElement("string", new XAttribute("name", "Face"),  face),
                        new XElement("string", new XAttribute("name", "Hat1"),  hat1),
                        new XElement("string", new XAttribute("name", "Hat2"),  hat2),
                        new XElement("string", new XAttribute("name", "Hat3"),  hat3),
                        new XElement("string", new XAttribute("name", "Shirt"), shirt),
                        new XElement("string", new XAttribute("name", "Pants"), pants),
                        new XElement("int",   new XAttribute("name", "CharacterAppearanceId"), placeId)
                    )
                )
            )
        );
        return Results.Content(xml.ToString(), "application/xml");
    }

    private static IResult BodyColors(HttpContext ctx)
    {
        // BodyColors.ashx returns a single int (24-bit RGB) for the
        // whole palette, encoded as <roblox><int name="BodyColors">...</int></roblox>.
        return Results.Content(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<roblox><int name=\"BodyColors\">" + DefaultBodyColorInt() + "</int></roblox>",
            "application/xml");
    }

    /// <summary>
    /// Look up a top-level value in the avatar JSON. Returns the
    /// boxed primitive (long, string) or null if the key is missing.
    /// </summary>
    private static object? TryGetValue(JsonElement? el, string key)
    {
        if (el is null) return null;
        if (el.Value.ValueKind != JsonValueKind.Object) return null;
        if (!el.Value.TryGetProperty(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetInt64(),
            _                    => v.ToString()
        };
    }

    /// <summary>
    /// Look up a nested dict in the avatar JSON (e.g. body_colors).
    /// Returns a long-keyed dictionary suitable for indexer access.
    /// </summary>
    private static Dictionary<string, long>? TryGetDict(JsonElement? el, string key)
    {
        if (el is null) return null;
        if (el.Value.ValueKind != JsonValueKind.Object) return null;
        if (!el.Value.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind != JsonValueKind.Object) return null;

        var dict = new Dictionary<string, long>();
        foreach (var prop in v.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var n))
            {
                dict[prop.Name] = n;
            }
        }
        return dict;
    }

    private static long ColorValue(Dictionary<string, long> colors, string key) =>
        colors.TryGetValue(key, out var v) ? v : DefaultBodyColorInt();

    private static Dictionary<string, long> DefaultBodyColorsDict() => new()
    {
        ["body"]      = DefaultBodyColorInt(),
        ["left_arm"]  = DefaultBodyColorInt(),
        ["right_arm"] = DefaultBodyColorInt(),
        ["left_leg"]  = DefaultBodyColorInt(),
        ["right_leg"] = DefaultBodyColorInt(),
        ["head"]      = DefaultBodyColorInt(),
    };

    private static int DefaultBodyColorInt() => 0xF2C189;

    private static string DefaultAppearance(long userId)
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
               "<roblox><Item class=\"CharacterAppearance\"><Properties>" +
               $"<int name=\"CharacterAppearanceId\">{userId}</int>" +
               "</Properties></Item></roblox>";
    }
}
