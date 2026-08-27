using System.Text.Json;

namespace Lo.Website.Models;

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

    public string Visibility { get; set; } = "n";
    public string? ThumbHash { get; set; }
    public string? StoragePath { get; set; }
    public string? MimeType { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
