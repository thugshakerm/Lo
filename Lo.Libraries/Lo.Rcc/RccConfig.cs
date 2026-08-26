namespace Lo.Rcc;

/// <summary>
/// Bound from the "Rcc" section of appsettings.json. Defines where
/// the RCCService SOAP endpoint is and how long requests are allowed
/// to take. This is the same config the website reads.
/// </summary>
public class RccConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 64989;
    public int TimeoutSeconds { get; set; } = 30;
}
