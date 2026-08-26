using Lo.Rcc;

namespace Lo.Website.Code.Data;

/// <summary>
/// Bridge between the Lo.Rcc library's IRccCallback interface and
/// the website's AppDb. Lo.Rcc doesn't know about our database
/// (it's a leaf library), so it calls into this adapter, which
/// then writes the data via the same Npgsql wrapper the
/// controllers use.
///
/// This mirrors how Roblox.Website's PersistenceBridge hooked
/// Roblox.Rcc into Roblox.DataAccess without the Rcc library
/// needing a database dependency.
/// </summary>
public class RccCallback : IRccCallback
{
    private readonly AppDb _db;

    public RccCallback(AppDb db)
    {
        _db = db;
    }

    public Task OnJobOpenedAsync(string jobId, long placeId, double expirationInSeconds)
    {
        return _db.UpsertGameServerAsync(new Lo.Website.Models.GameServer
        {
            JobId         = jobId,
            PlaceId       = placeId,
            Status        = "starting",
            LeaseExpiresAt = DateTime.UtcNow.AddSeconds(expirationInSeconds),
            LastPingAt    = DateTime.UtcNow,
        });
    }

    public Task OnSoapFaultAsync(string method, string fault, string? detail)
    {
        return _db.LogSoapFaultAsync(method, fault, detail);
    }
}
