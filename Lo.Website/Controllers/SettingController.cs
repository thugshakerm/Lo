using System.Text.Json;
using Lo.Website.Code.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lo.Website.Controllers;

public static class SettingController
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/v1/settings/application", ApplicationSettings);
        g.MapGet("/Setting/QuietGet/{bucket}", QuietGet);
    }

    private static IResult ApplicationSettings(RevivalConfig cfg)
    {
        return Results.Json(LoadFflags(cfg));
    }

    private static IResult QuietGet(string bucket, RevivalConfig cfg)
    {
        return Results.Json(LoadFflags(cfg));
    }

    private static Dictionary<string, object> LoadFflags(RevivalConfig cfg)
    {
        var path = Path.Combine(@"C:\lo\storage", cfg.Fflags.Path);
        if (!File.Exists(path))
        {

            return new Dictionary<string, object>
            {
                ["FFlagDebugBuildMode"]         = "False",
                ["FFlagDebugDisableTelemetry"]  = "True",
                ["DFFlagDebugDisableTelemetry"] = "True",
            };
        }
        try
        {
            var raw = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
            return parsed ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }
}
