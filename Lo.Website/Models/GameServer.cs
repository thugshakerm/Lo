namespace Lo.Website.Models;

/// <summary>
/// A live game server (a single RCC job).
/// </summary>
public class GameServer
{
    public long Id { get; set; }
    public string JobId { get; set; } = "";
    public long PlaceId { get; set; }
    public int Port { get; set; }
    public int MaxPlayers { get; set; }
    public bool PrivateServer { get; set; }
    /// <summary>One of: starting, running, shutting_down, dead.</summary>
    public string Status { get; set; } = "starting";
    public DateTime LeaseExpiresAt { get; set; }
    public DateTime LastPingAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
