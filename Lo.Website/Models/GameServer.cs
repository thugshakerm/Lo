namespace Lo.Website.Models;

public class GameServer
{
    public long Id { get; set; }
    public string JobId { get; set; } = "";
    public long PlaceId { get; set; }
    public int Port { get; set; }
    public int MaxPlayers { get; set; }
    public bool PrivateServer { get; set; }

    public string Status { get; set; } = "starting";
    public DateTime LeaseExpiresAt { get; set; }
    public DateTime LastPingAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
