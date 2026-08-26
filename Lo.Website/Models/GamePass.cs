namespace Lo.Website.Models;

/// <summary>
/// A game pass. Backed by an Asset (asset_type 34).
/// </summary>
public class GamePass
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public long CreatorId { get; set; }
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
