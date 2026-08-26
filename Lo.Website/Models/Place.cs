namespace Lo.Website.Models;

/// <summary>
/// A Place (a single game server template). A Universe is a logical
/// group of Places; the 2018M client talks about places, not
/// universes.
/// </summary>
public class Place
{
    public long Id { get; set; }
    public long UniverseId { get; set; }
    public long CreatorId { get; set; }
    public string Name { get; set; } = "";
    public int MaxPlayers { get; set; } = 20;
    public bool R15Morphing { get; set; }
    public string? StoragePath { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
