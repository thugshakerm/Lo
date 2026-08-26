namespace Lo.Website.Models;

/// <summary>
/// The asset_ownership join table. Many-to-many: users own assets,
/// assets have many owners.
/// </summary>
public class AssetOwnership
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public long UserId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
