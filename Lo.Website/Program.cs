using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Lo.Website.Code.Config;
using Lo.Website.Code.Middleware;
using Lo.Rcc;
using Lo.Website.Code.Sign;

// ─────────────────────────────────────────────────────────────────────
// Lo Revival — 2018M protocol-compatible API
// ─────────────────────────────────────────────────────────────────────
//
// ASP.NET Core 8 Minimal API. The 5 subdomains are all routed to the
// same Kestrel process; subdomain dispatch is done in middleware so
// that one binding per IP is enough (the request's Host: header
// tells us which subdomain it was for).
//
// The Laravel routes/rbx.php was the source of truth for the URLs
// and handler set. Every endpoint in there is mapped here.
// ─────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────
var revival = builder.Configuration.GetSection("Revival").Get<RevivalConfig>()
    ?? new RevivalConfig();
var rcc = builder.Configuration.GetSection("Rcc").Get<RccConfig>()
    ?? new RccConfig();
builder.Services.AddSingleton(revival);
builder.Services.AddSingleton(rcc);

// ── Logging ─────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(opts =>
{
    opts.IncludeScopes = false;
    opts.SingleLine = true;
    opts.TimestampFormat = "HH:mm:ss ";
});

// ── Database ────────────────────────────────────────────────────────
builder.Services.AddSingleton<Lo.Website.Code.Data.AppDb>(sp =>
{
    var connStr = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=127.0.0.1;Port=5432;Database=lo;Username=lo;Password=lo";
    return new Lo.Website.Code.Data.AppDb(connStr);
});

// ── Signing layer ───────────────────────────────────────────────────
builder.Services.AddSingleton<KeyManager>();
builder.Services.AddSingleton<SecurityNotary>();

// ── RCC client (SOAP via HttpClient) ────────────────────────────────
// Register RccCallback as the IRccCallback implementation. The
// RccClient takes it via DI; when it opens a job, the callback
// writes the row via AppDb.
builder.Services.AddSingleton<Lo.Rcc.IRccCallback, Lo.Website.Code.Data.RccCallback>();
builder.Services.AddHttpClient<RccClient>((sp, http) =>
{
    http.BaseAddress = new Uri($"http://{rcc.Host}:{rcc.Port}/");
    http.Timeout = TimeSpan.FromSeconds(rcc.TimeoutSeconds);
});

// ── JSON options ────────────────────────────────────────────────────
builder.Services.Configure<JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// ── Middleware pipeline ─────────────────────────────────────────────
app.UseMiddleware<SubdomainRouter>();   // sets HttpContext.Items["subdomain"]
app.UseMiddleware<SetClientIp>();       // sets HttpContext.Items["realIp"]
app.UseMiddleware<RateLimit>();         // crude per-IP rate limit
app.UseRouting();
app.UseMiddleware<SubdomainGuard>();    // after routing, reject mismatches

// ── Health & Root ───────────────────────────────────────────────────
app.MapGet("/healthz", () => Results.Text("ok", "text/plain"));
app.MapGet("/", () => Results.Json(new { service = "Lo Revival", era = "2018M", status = "online" }));

// ── 5 subdomain groups (matching routes/rbx.php 1:1) ────────────────
//
// Each route group carries a SubdomainKey metadata; the
// SubdomainGuard middleware rejects requests whose Host: header
// subdomain doesn't match the route's required subdomain.

RouteGroupBuilder Www() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("www"));
RouteGroupBuilder Api() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("api"));
RouteGroupBuilder AssetGame() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("assetgame"));
RouteGroupBuilder ClientSettings() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("clientsettingscdn"));
RouteGroupBuilder Compat() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("applicationcompatibility"));

// ── www.<domain> + assetgame.<domain> (game/asset/login) ────────────
Lo.Website.Controllers.AuthController.Map(Www());
Lo.Website.Controllers.AuthController.Map(AssetGame());

Lo.Website.Controllers.GameController.Map(Www());
Lo.Website.Controllers.GameController.Map(AssetGame());

Lo.Website.Controllers.GameServerApiController.Map(Www());
Lo.Website.Controllers.GameServerApiController.Map(AssetGame());

Lo.Website.Controllers.AssetController.Map(Www());
Lo.Website.Controllers.AssetController.Map(AssetGame());

Lo.Website.Controllers.AvatarController.Map(Www());
Lo.Website.Controllers.AvatarController.Map(AssetGame());

Lo.Website.Controllers.ThumbnailController.Map(Www());
Lo.Website.Controllers.ThumbnailController.Map(AssetGame());

Lo.Website.Controllers.InsertController.Map(Www());
Lo.Website.Controllers.InsertController.Map(AssetGame());

Lo.Website.Controllers.LuaWebController.Map(Www());
Lo.Website.Controllers.LuaWebController.Map(AssetGame());

Lo.Website.Controllers.GamePassController.Map(Www());
Lo.Website.Controllers.GamePassController.Map(AssetGame());

Lo.Website.Controllers.BadgesController.Map(Www());
Lo.Website.Controllers.BadgesController.Map(AssetGame());

// ── api.<domain> ────────────────────────────────────────────────────
Lo.Website.Controllers.PlaceController.Map(Api());
Lo.Website.Controllers.MarketplaceController.Map(Api());

// ── clientsettingscdn.<domain> ──────────────────────────────────────
Lo.Website.Controllers.SettingController.Map(ClientSettings());

// ── applicationcompatibility.<domain> ───────────────────────────────
Lo.Website.Controllers.CompatibilityController.Map(Compat());

// ── Listen on port 8080 if running standalone outside IIS ───────────
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANCM_LAUNCH_RESULT_PATH")))
{
    app.Urls.Clear();
    app.Urls.Add("http://0.0.0.0:8080");
}

// Helpful startup banner
app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = string.Join(", ", app.Urls);
    app.Logger.LogInformation("Lo.Website listening on {Urls}", urls);
    app.Logger.LogInformation("Domain: {Domain} (length={Len})", revival.Domain, revival.DomainLength);
    app.Logger.LogInformation("Subdomains: www={W} api={A} assetgame={AG} clientsettingscdn={CS} applicationcompatibility={AC}",
        revival.Subdomains.Web, revival.Subdomains.Api, revival.Subdomains.AssetGame,
        revival.Subdomains.ClientSettings, revival.Subdomains.Compat);
});

app.Run();
