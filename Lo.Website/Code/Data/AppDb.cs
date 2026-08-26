using System.Text.Json;
using Lo.Website.Models;
using Npgsql;

namespace Lo.Website.Code.Data;

/// <summary>
/// Thin wrapper around Npgsql. The original PHP code uses Eloquent;
/// we don't need an ORM for the simple queries the revival makes.
/// Each method maps to a single SQL statement.
///
/// All methods are async (the original PHP code is sync; this is a
/// strict improvement on the Kestrel thread-pool side).
/// </summary>
public class AppDb
{
    private readonly string _connStr;

    public AppDb(string connStr)
    {
        _connStr = connStr;
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var c = new NpgsqlConnection(_connStr);
        await c.OpenAsync();
        return c;
    }

    public async Task<User?> FindUserAsync(long id)
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, email, password, avatar, banned_until, banned_reason, created_at, updated_at FROM users WHERE id = @id",
            c);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return ReadUser(r);
    }

    public async Task<Asset?> FindAssetAsync(long id)
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, description, asset_type, creator_id, price, is_for_sale, is_approved, visibility, thumb_hash, storage_path, mime_type, created_at, updated_at FROM assets WHERE id = @id",
            c);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return ReadAsset(r);
    }

    public async Task<List<Asset>> ListAssetsByTypeAsync(int? assetType, string visibility, bool isApproved, int page, int size)
    {
        await using var c = await OpenAsync();
        var sql = "SELECT id, name, description, asset_type, creator_id, price, is_for_sale, is_approved, visibility, thumb_hash, storage_path, mime_type, created_at, updated_at FROM assets WHERE visibility = @vis AND is_approved = @appr";
        if (assetType.HasValue) sql += " AND asset_type = @type";
        sql += " ORDER BY id LIMIT @lim OFFSET @off";
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("vis", visibility);
        cmd.Parameters.AddWithValue("appr", isApproved);
        cmd.Parameters.AddWithValue("lim", Math.Min(size, 100));
        cmd.Parameters.AddWithValue("off", Math.Max(0, (page - 1) * size));
        if (assetType.HasValue) cmd.Parameters.AddWithValue("type", assetType.Value);
        var list = new List<Asset>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(ReadAsset(r));
        return list;
    }

    public async Task<Place?> FindPlaceAsync(long id)
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, universe_id, creator_id, name, max_players, r15_morphing, storage_path FROM places WHERE id = @id",
            c);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new Place
        {
            Id = r.GetInt64(0),
            UniverseId = r.GetInt64(1),
            CreatorId = r.GetInt64(2),
            Name = r.GetString(3),
            MaxPlayers = r.GetInt32(4),
            R15Morphing = r.GetBoolean(5),
            StoragePath = r.IsDBNull(6) ? null : r.GetString(6),
        };
    }

    public async Task UpsertGameServerAsync(GameServer gs)
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO game_servers (job_id, place_id, port, max_players, private_server, status, lease_expires_at, last_ping_at, updated_at)
            VALUES (@job, @place, @port, @maxp, @priv, @status, @lease, @ping, now())
            ON CONFLICT (job_id) DO UPDATE
              SET place_id = EXCLUDED.place_id,
                  port = EXCLUDED.port,
                  max_players = EXCLUDED.max_players,
                  private_server = EXCLUDED.private_server,
                  status = EXCLUDED.status,
                  lease_expires_at = EXCLUDED.lease_expires_at,
                  last_ping_at = EXCLUDED.last_ping_at,
                  updated_at = now()
        ", c);
        cmd.Parameters.AddWithValue("job", gs.JobId);
        cmd.Parameters.AddWithValue("place", gs.PlaceId);
        cmd.Parameters.AddWithValue("port", gs.Port);
        cmd.Parameters.AddWithValue("maxp", gs.MaxPlayers);
        cmd.Parameters.AddWithValue("priv", gs.PrivateServer);
        cmd.Parameters.AddWithValue("status", gs.Status);
        cmd.Parameters.AddWithValue("lease", gs.LeaseExpiresAt);
        cmd.Parameters.AddWithValue("ping", gs.LastPingAt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> UserOwnsGamePassAsync(long userId, long passId)
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT EXISTS(
                SELECT 1 FROM game_passes gp
                JOIN asset_ownership ao ON ao.asset_id = gp.asset_id
                WHERE gp.asset_id = @pass AND ao.user_id = @user
            )", c);
        cmd.Parameters.AddWithValue("user", userId);
        cmd.Parameters.AddWithValue("pass", passId);
        var v = await cmd.ExecuteScalarAsync();
        return v is bool b && b;
    }

    public async Task LogSoapFaultAsync(string method, string fault, string? detail)
    {
        try
        {
            await using var c = await OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO rcc_soap_faults (method, fault, detail, occurred_at) VALUES (@m, @f, @d, now())", c);
            cmd.Parameters.AddWithValue("m", method);
            cmd.Parameters.AddWithValue("f", fault);
            cmd.Parameters.AddWithValue("d", (object?)detail ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Don't throw from the logger.
        }
    }

    public async Task LogAuditAsync(string event_, long? userId, string? ip, string? detail)
    {
        try
        {
            await using var c = await OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO audit_log (event, user_id, ip, detail, occurred_at) VALUES (@e, @u, @i, @d, now())", c);
            cmd.Parameters.AddWithValue("e", event_);
            cmd.Parameters.AddWithValue("u", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("i", (object?)ip ?? DBNull.Value);
            cmd.Parameters.AddWithValue("d", (object?)detail ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
        }
    }

    // ── row readers ───────────────────────────────────────────────

    private static User ReadUser(NpgsqlDataReader r)
    {
        JsonElement? avatar = null;
        if (!r.IsDBNull(4))
        {
            var raw = r.GetString(4);
            if (!string.IsNullOrEmpty(raw))
            {
                try { avatar = JsonDocument.Parse(raw).RootElement.Clone(); }
                catch { avatar = null; }
            }
        }
        return new User
        {
            Id = r.GetInt64(0),
            Name = r.IsDBNull(1) ? "" : r.GetString(1),
            Email = r.IsDBNull(2) ? null : r.GetString(2),
            Password = r.IsDBNull(3) ? null : r.GetString(3),
            Avatar = avatar,
            BannedUntil = r.IsDBNull(5) ? null : r.GetDateTime(5),
            BannedReason = r.IsDBNull(6) ? null : r.GetString(6),
            CreatedAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
            UpdatedAt = r.IsDBNull(8) ? null : r.GetDateTime(8),
        };
    }

    private static Asset ReadAsset(NpgsqlDataReader r)
    {
        return new Asset
        {
            Id = r.GetInt64(0),
            Name = r.IsDBNull(1) ? "" : r.GetString(1),
            Description = r.IsDBNull(2) ? null : r.GetString(2),
            AssetType = r.GetInt32(3),
            CreatorId = r.GetInt64(4),
            Price = r.GetInt32(5),
            IsForSale = r.GetBoolean(6),
            IsApproved = r.GetBoolean(7),
            Visibility = r.IsDBNull(8) ? "n" : r.GetString(8),
            ThumbHash = r.IsDBNull(9) ? null : r.GetString(9),
            StoragePath = r.IsDBNull(10) ? null : r.GetString(10),
            MimeType = r.IsDBNull(11) ? null : r.GetString(11),
            CreatedAt = r.IsDBNull(12) ? null : r.GetDateTime(12),
            UpdatedAt = r.IsDBNull(13) ? null : r.GetDateTime(13),
        };
    }
}
