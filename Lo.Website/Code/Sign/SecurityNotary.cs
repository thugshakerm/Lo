using System.Security.Cryptography;
using System.Text;

namespace Lo.Website.Code.Sign;

/// <summary>
/// The script-signing class. Produces Roblox-compatible signed bodies
/// for the 2018M client.
///
/// The signature is RSA-1024 + SHA-1 over the script body (with a
/// leading \r\n to match the original Roblox signing pipeline).
///
/// The 2018M client verifies the signature against the embedded
/// public key (the CAPI blob, pasted in via the binary patch). The
/// signature format is identical to the leak Finobe was based on
/// (the 2019 Web Assemblies backend leak).
///
/// Source: wiki/knowledge/signatures.md, Finobe's
/// app/Http/Controllers/SecurityNotary.php.
/// </summary>
public class SecurityNotary
{
    private readonly KeyManager _keys;
    private readonly ILogger<SecurityNotary> _log;

    public SecurityNotary(KeyManager keys, ILogger<SecurityNotary> log)
    {
        _keys = keys;
        _log = log;
    }

    /// <summary>
    /// Sign a script body. Returns the wrapped body (signature prefix + script).
    /// The leading \r\n matters: the client verifies by excluding the
    /// %sig% / --rbxsig prefix from the hash, but the body it hashes
    /// must start with \r\n to match what the original Roblox signing
    /// pipeline produced.
    /// </summary>
    public string SignScript(string script, FormatVersion version = FormatVersion.V2, bool withNewline = true)
    {
        if (withNewline) script = "\r\n" + script;
        var sig = CreateSignature(script);
        if (string.IsNullOrEmpty(sig))
        {
            _log.LogError("SecurityNotary: signature was empty; serving unsigned body");
            return script;
        }
        return version == FormatVersion.V1
            ? $"%{sig}%{script}"
            : $"--rbxsig{sig}%{script}";
    }

    /// <summary>
    /// Sign a CoreGui-style wrapped body. The body already has the
    /// %{assetId}% prefix; we add the signature on top.
    ///
    /// Example input:  "%1234%\r\nprint('hi')"
    /// Example output: "%sig%%1234%\r\nprint('hi')"  (V1)
    ///                 "--rbxsig%sig%%1234%\r\nprint('hi')"  (V2)
    /// </summary>
    public string SignCoreGuiBody(string wrappedScript, FormatVersion version = FormatVersion.V2)
    {
        var sig = CreateSignature(wrappedScript);
        if (string.IsNullOrEmpty(sig))
        {
            _log.LogError("SecurityNotary: signature was empty for CoreGui body");
            return wrappedScript;
        }
        return version == FormatVersion.V1
            ? $"%{sig}%{wrappedScript}"
            : $"--rbxsig{sig}%{wrappedScript}";
    }

    private string CreateSignature(string message)
    {
        try
        {
            var rsa = _keys.PrivateKey();
            var sig = rsa.SignData(
                Encoding.UTF8.GetBytes(message),
                _keys.Algorithm(),
                RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sig);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SecurityNotary: RSA signing failed");
            return "";
        }
    }
}
