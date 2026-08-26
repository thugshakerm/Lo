using System.Security.Cryptography;
using Lo.Website.Code.Config;

namespace Lo.Website.Code.Sign;

/// <summary>
/// The two signature wrapper formats used by Roblox clients.
///
/// V1: %signature%script       (2009 - mid-2013)
/// V2: --rbxsig%signature%script (late-2013 - 2018E)
///
/// 2018M uses V2.
///
/// Source: wiki/knowledge/signatures.md (Finobe's
/// app/Http/Controllers/SecurityNotary.php).
/// </summary>
public enum FormatVersion
{
    V1,
    V2
}

/// <summary>
/// Loads the RSA-1024 private key used to sign scripts.
///
/// The key lives at C:\lo\storage\privateKey1024.pem and is NOT committed.
/// To generate one:
///
///   openssl genrsa -out C:\lo\storage\privateKey1024.pem 1024
///   openssl rsa -in C:\lo\storage\privateKey1024.pem -pubout -outform DER | base64 -w 0 > C:\lo\storage\publicKeyBlob.txt
///
/// The public blob is the CAPI-format string pasted into the patched
/// client binary at the BgIAA... location.
/// </summary>
public class KeyManager
{
    private readonly RevivalConfig _cfg;
    private RSA? _rsa;
    private readonly object _lock = new();

    public KeyManager(RevivalConfig cfg)
    {
        _cfg = cfg;
    }

    public RSA PrivateKey()
    {
        if (_rsa != null) return _rsa;
        lock (_lock)
        {
            if (_rsa != null) return _rsa;
            var path = _cfg.Signing.PrivateKeyPath;
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"RSA private key not found at {path}. Generate one with: " +
                    $"openssl genrsa -out {path} 1024");
            }
            var pem = File.ReadAllText(path);
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            _rsa = rsa;
            return _rsa;
        }
    }

    /// <summary>
    /// Returns the CAPI-format public blob (base64 of the DER-encoded
    /// public key). Loaded from disk if present, else derived from
    /// the private key.
    /// </summary>
    public string PublicBlob()
    {
        var path = _cfg.Signing.PublicBlobPath;
        if (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }
        // Derive from the private key.
        var pub = PrivateKey().ExportSubjectPublicKeyInfo();
        // Convert to the CAPI format (X509 SubjectPublicKeyInfo wrapped
        // in RSAPUBKEY) and base64. For simplicity, we just base64 the
        // SubjectPublicKeyInfo - the patched client is flexible about
        // which public key format it accepts as long as it parses.
        return Convert.ToBase64String(pub);
    }

    public HashAlgorithmName Algorithm() =>
        _cfg.Signing.Algorithm?.ToLowerInvariant() switch
        {
            "sha256" => HashAlgorithmName.SHA256,
            _        => HashAlgorithmName.SHA1,
        };
}
