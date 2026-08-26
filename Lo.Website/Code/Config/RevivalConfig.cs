namespace Lo.Website.Code.Config;

/// <summary>
/// Top-level config bound from appsettings.json "Revival" section.
/// Mirrors the PHP config/rbx.php values that the Laravel controllers read.
///
/// Note: the "Rcc" subsection of appsettings.json is bound to
/// Lo.Rcc.RccConfig (defined in the Lo.Rcc library) — the website
/// project references it via a ProjectReference. We re-export it
/// here under the alias <see cref="RccConnectionConfig"/> for
/// backwards-compat with internal code that referenced it as
/// "RevivalConfig.Rcc" in the old Lo.Api era.
/// </summary>
public class RevivalConfig
{
    public string Domain { get; set; } = "gazeee.xyz";
    public int DomainLength { get; set; } = 10;
    public SubdomainConfig Subdomains { get; set; } = new();
    public FflagsConfig Fflags { get; set; } = new();
    public LuaConfig Lua { get; set; } = new();
    public SigningConfig Signing { get; set; } = new();
    /// <summary>Default values used when opening new RCC jobs (lease, cores).</summary>
    public RccDefaultsConfig Rcc { get; set; } = new();
}

public class SubdomainConfig
{
    public string Web { get; set; } = "www";
    public string Api { get; set; } = "api";
    public string AssetGame { get; set; } = "assetgame";
    public string ClientSettings { get; set; } = "clientsettingscdn";
    public string Compat { get; set; } = "applicationcompatibility";
}

public class FflagsConfig
{
    public string Path { get; set; } = "rbx/fflags/2018M.json";
}

public class LuaConfig
{
    public string Version { get; set; } = "0.412.0.412";
}

public class SigningConfig
{
    public string PrivateKeyPath { get; set; } = "C:\\lo\\storage\\privateKey1024.pem";
    public string PublicBlobPath { get; set; } = "C:\\lo\\storage\\publicKeyBlob.txt";
    public string Algorithm { get; set; } = "sha1";
}

public class RccDefaultsConfig
{
    public int DefaultLeaseSeconds { get; set; } = 600;
    public double DefaultCores { get; set; } = 1.0;
}
