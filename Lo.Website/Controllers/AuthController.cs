using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Lo.Website.Code.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

/// <summary>
/// /Login/* — client authentication.
///
/// The 2018M client calls /Login/Negotiate.ashx after the user
/// authenticates with the website. The response is a session token
/// the client uses as an "auth ticket" on subsequent requests.
///
/// In a 2018L+ patched client, the "ProcessTicket exception" check
/// is JMP'd out (see wiki/Research/patch tickets on 2018L+.txt), so
/// verification is a no-op on the binary side. We still mint a
/// session token here and honor RBX_AUTH / .ROBLOSECURITY cookies
/// when present so that banned users get rejected at the join step.
/// </summary>
public static class AuthController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/Login/Negotiate.ashx", Negotiate);
        g.MapMethods("/Login/Default.aspx", new[] { "GET", "POST" }, Default);
        g.MapMethods("/Login/Logout.ashx",  new[] { "GET", "POST" }, Logout);
    }

    /// <summary>
    /// GET /Login/Negotiate.ashx
    /// Returns an XML body with the user's session ticket.
    /// </summary>
    private static IResult Negotiate(HttpContext ctx, AppDb db)
    {
        var userId = ResolveUserId(ctx);

        if (userId == 0)
        {
            return Results.Content(BuildXml(0, "Guest", $"guest-{RandomToken(16)}"),
                "application/xml");
        }

        // The user's actually authenticated; honor them
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
        // Honor .ROBLOSECURITY or RBX_AUTH cookie
        var cookie = ctx.Request.Cookies[".ROBLOSECURITY"] ?? ctx.Request.Cookies["RBX_AUTH"];
        if (!string.IsNullOrEmpty(cookie))
        {
            // The PHP app's AuthController did the same parse; we accept
            // any cookie value containing "user-{id}-" as a hint.
            if (cookie.StartsWith("user-"))
            {
                var parts = cookie.Split('-');
                if (parts.Length >= 2 && long.TryParse(parts[1], out var id)) return id;
            }
        }
        // Or the ?userId query parameter
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
