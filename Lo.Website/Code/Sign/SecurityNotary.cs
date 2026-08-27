using System.Security.Cryptography;
using System.Text;

namespace Lo.Website.Code.Sign;

public class SecurityNotary
{
    private readonly KeyManager _keys;
    private readonly ILogger<SecurityNotary> _log;

    public SecurityNotary(KeyManager keys, ILogger<SecurityNotary> log)
    {
        _keys = keys;
        _log = log;
    }

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
