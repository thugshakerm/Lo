using System.Text.Json;

namespace Lo.Website.Models;

/// <summary>
/// The universal Asset table.
///
/// Roblox's catalog is one table with an `asset_type` column
/// distinguishing hats (8), t-shirts (2), shirts (11), pants (12),
/// faces (18), heads (17), audio (3), models (10), packages (32),
/// game passes (34), decals (13), etc. The 2018M client sends and
/// receives asset IDs as plain integers.
/// </summary>
public class Asset
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int AssetType { get; set; }
    public long CreatorId { get; set; }
    public int Price { get; set; }
    public bool IsForSale { get; set; }
    public bool IsApproved { get; set; }
    /// <summary>Visibility: "n"=public, "u"=unlisted, "p"=private.</summary>
    public string Visibility { get; set; } = "n";
    public string? ThumbHash { get; set; }
    public string? StoragePath { get; set; }
    public string? MimeType { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
