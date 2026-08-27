namespace Lo.Website.Models;

public class AssetOwnership
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public long UserId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
