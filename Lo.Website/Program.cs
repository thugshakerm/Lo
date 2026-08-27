using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Lo.Website.Code.Config;
using Lo.Website.Code.Middleware;
using Lo.Rcc;
using Lo.Website.Code.Sign;

var builder = WebApplication.CreateBuilder(args);

var revival = builder.Configuration.GetSection("Revival").Get<RevivalConfig>()
    ?? new RevivalConfig();
var rcc = builder.Configuration.GetSection("Rcc").Get<RccConfig>()
    ?? new RccConfig();
builder.Services.AddSingleton(revival);
builder.Services.AddSingleton(rcc);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(opts =>
{
    opts.IncludeScopes = false;
    opts.SingleLine = true;
    opts.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddSingleton<Lo.Website.Code.Data.AppDb>(sp =>
{
    var connStr = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=127.0.0.1;Port=5432;Database=lo;Username=lo;Password=lo";
    return new Lo.Website.Code.Data.AppDb(connStr);
});

builder.Services.AddSingleton<KeyManager>();
builder.Services.AddSingleton<SecurityNotary>();

builder.Services.AddSingleton<Lo.Rcc.IRccCallback, Lo.Website.Code.Data.RccCallback>();
builder.Services.AddHttpClient<RccClient>((sp, http) =>
{
    http.BaseAddress = new Uri($"http://{rcc.Host}:{rcc.Port}/");
    http.Timeout = TimeSpan.FromSeconds(rcc.TimeoutSeconds);
});

builder.Services.Configure<JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();
app.UseDeveloperExceptionPage();

app.UseMiddleware<SubdomainRouter>();
app.UseMiddleware<SetClientIp>();
app.UseMiddleware<RateLimit>();
app.UseRouting();
app.UseMiddleware<SubdomainGuard>();

app.MapGet("/healthz", () => Results.Text("ok", "text/plain"));
app.MapGet("/", () => Results.Json(new { service = "Lo Revival", era = "2018M", status = "online" }));

RouteGroupBuilder WwwAndAssetGame() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("www", "assetgame"));
RouteGroupBuilder Api() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("api"));
RouteGroupBuilder ClientSettings() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("clientsettingscdn"));
RouteGroupBuilder Compat() => app.MapGroup("/")
    .WithMetadata(new SubdomainKey("applicationcompatibility"));

var gameGroup = WwwAndAssetGame();
Lo.Website.Controllers.AuthController.Map(gameGroup);
Lo.Website.Controllers.GameController.Map(gameGroup);
Lo.Website.Controllers.GameServerApiController.Map(gameGroup);
Lo.Website.Controllers.AssetController.Map(gameGroup);
Lo.Website.Controllers.AvatarController.Map(gameGroup);
Lo.Website.Controllers.ThumbnailController.Map(gameGroup);
Lo.Website.Controllers.InsertController.Map(gameGroup);
Lo.Website.Controllers.LuaWebController.Map(gameGroup);
Lo.Website.Controllers.GamePassController.Map(gameGroup);
Lo.Website.Controllers.BadgesController.Map(gameGroup);

var apiGroup = Api();
Lo.Website.Controllers.PlaceController.Map(apiGroup);
Lo.Website.Controllers.MarketplaceController.Map(apiGroup);

Lo.Website.Controllers.SettingController.Map(ClientSettings());

Lo.Website.Controllers.CompatibilityController.Map(Compat());

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
