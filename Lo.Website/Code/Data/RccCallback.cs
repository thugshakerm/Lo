using Lo.Rcc;

namespace Lo.Website.Code.Data;

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
