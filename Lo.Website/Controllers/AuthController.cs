using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

public static class AuthController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/Login/Negotiate.ashx", Negotiate);
        g.MapMethods("/Login/Default.aspx", new[] { "GET", "POST" }, Default);
        g.MapMethods("/Login/Logout.ashx",  new[] { "GET", "POST" }, Logout);
    }

    private static IResult Negotiate(HttpContext ctx, AppDb db)
    {
        var userId = ResolveUserId(ctx);

        if (userId == 0)
        {
            return Results.Content(BuildXml(0, "Guest", $"guest-{RandomToken(16)}"),
                "application/xml");
        }

        var user = db.FindUserAsync(userId).GetAwaiter().GetResult();
        if (user is null)
        {
            return Results.Content(BuildXml(0, "Guest", $"guest-{RandomToken(16)}"),
                "application/xml");
        }
        if (user.IsBanned())
        {
            return Results.Content(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>403</Code><Message>This account is banned.</Message></Error>",
                "application/xml", null, (int)System.Net.HttpStatusCode.Forbidden);
        }
        return Results.Content(BuildXml(user.Id, user.Name, $"user-{user.Id}-{RandomToken(8)}"),
            "application/xml");
    }

    private static IResult Default(HttpContext ctx) =>
        Results.Content(BuildXml(0, "Guest", $"guest-{RandomToken(16)}"), "application/xml");

    private static IResult Logout(HttpContext ctx) =>
        Results.Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?><ok/>", "application/xml");

    private static long ResolveUserId(HttpContext ctx)
    {

        var cookie = ctx.Request.Cookies[".ROBLOSECURITY"] ?? ctx.Request.Cookies["RBX_AUTH"];
        if (!string.IsNullOrEmpty(cookie))
        {

            if (cookie.StartsWith("user-"))
            {
                var parts = cookie.Split('-');
                if (parts.Length >= 2 && long.TryParse(parts[1], out var id)) return id;
            }
        }

        var q = ctx.Request.Query["userId"].ToString();
        if (long.TryParse(q, out var qid)) return qid;
        return 0;
    }

    private static string RandomToken(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToHexString(buf).ToLowerInvariant();
    }

    private static string BuildXml(long userId, string userName, string ticket)
    {
        XNamespace x = "http://www.roblox.com/";
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(x + "AuthNegotiation",
                new XElement(x + "Ticket", ticket),
                new XElement(x + "User",
                    new XElement(x + "Id", userId),
                    new XElement(x + "Name", userName))
            )
        ).ToString();
    }
}
