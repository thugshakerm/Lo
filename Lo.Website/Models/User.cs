using System.Text.Json;

namespace Lo.Website.Models;

public class User
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Password { get; set; }
    public JsonElement? Avatar { get; set; }
    public DateTime? BannedUntil { get; set; }
    public string? BannedReason { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsBanned() =>
        BannedUntil.HasValue && BannedUntil.Value > DateTime.UtcNow;
}
