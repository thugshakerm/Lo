using System.Security.Cryptography;
using Lo.Website.Code.Config;

namespace Lo.Website.Code.Sign;

public enum FormatVersion
{
    V1,
    V2
}

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

    public string PublicBlob()
    {
        var path = _cfg.Signing.PublicBlobPath;
        if (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }

        var pub = PrivateKey().ExportSubjectPublicKeyInfo();

        return Convert.ToBase64String(pub);
    }

    public HashAlgorithmName Algorithm() =>
        _cfg.Signing.Algorithm?.ToLowerInvariant() switch
        {
            "sha256" => HashAlgorithmName.SHA256,
            _        => HashAlgorithmName.SHA1,
        };
}
