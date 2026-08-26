namespace Lo.Rcc;

/// <summary>
/// Optional callback interface that Lo.Website registers with the
/// RccClient at DI time. The Rcc library itself doesn't know about
/// AppDb or GameServer — it just calls these hooks. This keeps
/// Lo.Rcc as a leaf library with no dependency on the website
/// project (mirroring Roblox.Libraries/Roblox.Rcc).
/// </summary>
public interface IRccCallback
{
    /// <summary>
    /// Called after a successful OpenJob to record the new game
    /// server in whatever persistence layer the host uses.
    /// </summary>
    Task OnJobOpenedAsync(string jobId, long placeId, double expirationInSeconds);

    /// <summary>
    /// Called when the RCC server returns a SOAP fault. The host
    /// typically persists these to a fault log table.
    /// </summary>
    Task OnSoapFaultAsync(string method, string fault, string? detail);
}
