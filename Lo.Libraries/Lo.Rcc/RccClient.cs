using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Lo.Rcc;

/// <summary>
/// Typed wrapper around the RCCService SOAP endpoint.
///
/// The original PHP code used \SoapClient driven by the WSDL at
/// C:\lo\storage\RCCService.wsdl. In .NET, the WCF equivalent is
/// heavyweight. Since we only call ~8 methods with a known wire
/// format, we hand-roll the SOAP body with XDocument and parse the
/// response the same way. This is ~80 lines instead of ~800, and
/// the wire format is identical.
///
/// Lo.Rcc is a leaf library with no dependencies on Lo.Website.
/// Persistence (logging faults, recording jobs) is delegated to
/// IRccCallback, which the website implements.
///
/// Source: wiki/rccservice/windows/how2rcc.md (SOAP method reference),
/// wiki/rccservice/windows/not-expiring-jobs.md (lease renewal pattern).
/// </summary>
public class RccClient
{
    private readonly HttpClient _http;
    private readonly IRccCallback _cb;
    private readonly ILogger<RccClient> _log;
    private readonly RccConfig _cfg;

    public RccClient(HttpClient http, IRccCallback cb, ILogger<RccClient> log, RccConfig cfg)
    {
        _http = http;
        _cb   = cb;
        _log  = log;
        _cfg  = cfg;
    }

    // ── Lifecycle ────────────────────────────────────────────────

    /// <summary>
    /// Open a new job on RCC. Spawns a child process and returns a jobId.
    /// Optionally runs a ScriptExecution on the new job before returning.
    /// </summary>
    public async Task<string?> OpenJobAsync(Job job, ScriptExecution? script = null, int placeId = 0)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("OpenJob",
                new XElement("job",
                    new XElement("id", job.Id),
                    new XElement("expirationInSeconds", job.ExpirationInSeconds),
                    new XElement("category", job.Category),
                    new XElement("cores", job.Cores)),
                script is null ? null : new XElement("script", BuildScript(script))
            ));

        var ok = await CallAsync(doc, "OpenJob");
        if (!ok) return null;

        await _cb.OnJobOpenedAsync(job.Id, placeId, job.ExpirationInSeconds);
        return job.Id;
    }

    public async Task<List<LuaValue>?> BatchJobAsync(Job job, ScriptExecution script)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("BatchJob",
                new XElement("job",
                    new XElement("id", job.Id),
                    new XElement("expirationInSeconds", job.ExpirationInSeconds),
                    new XElement("category", job.Category),
                    new XElement("cores", job.Cores)),
                new XElement("script", BuildScript(script))
            ));
        return await CallAndParseLuaValuesAsync(doc, "BatchJob");
    }

    public async Task<List<LuaValue>?> ExecuteAsync(string jobId, ScriptExecution script)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("Execute",
                new XElement("jobID", jobId),
                new XElement("script", BuildScript(script))
            ));
        return await CallAndParseLuaValuesAsync(doc, "Execute");
    }

    public async Task<double> RenewLeaseAsync(string jobId, int expirationInSeconds)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("RenewLease",
                new XElement("jobID", jobId),
                new XElement("expirationInSeconds", (double)expirationInSeconds)));
        var v = await CallAndReturnScalarAsync(doc, "RenewLease");
        return v is double d ? d : 0.0;
    }

    public async Task CloseJobAsync(string jobId)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("CloseJob", new XElement("jobID", jobId)));
        await CallAsync(doc, "CloseJob");
    }

    public async Task<double> GetExpirationAsync(string jobId)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("GetExpiration", new XElement("jobID", jobId)));
        var v = await CallAndReturnScalarAsync(doc, "GetExpiration");
        return v is double d ? d : 0.0;
    }

    public async Task<List<string>> GetAllJobsAsync()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("GetAllJobs"));
        var response = await SendAsync(doc);
        if (response is null) return new List<string>();
        var jobs = response.Descendants("job");
        return jobs.Select(j => (string?)j.Element("id") ?? "").Where(s => s.Length > 0).ToList();
    }

    public async Task<int> CloseExpiredJobsAsync()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("CloseExpiredJobs"));
        var v = await CallAndReturnScalarAsync(doc, "CloseExpiredJobs");
        return v is int i ? i : (int)((double?)v ?? 0);
    }

    public async Task<int> CloseAllJobsAsync()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("CloseAllJobs"));
        var v = await CallAndReturnScalarAsync(doc, "CloseAllJobs");
        return v is int i ? i : (int)((double?)v ?? 0);
    }

    // ── Status ───────────────────────────────────────────────────

    public async Task<string> HelloWorldAsync()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("HelloWorld"));
        var response = await SendAsync(doc);
        return response?.Descendants("HelloWorldResult").FirstOrDefault()?.Value ?? "";
    }

    public async Task<string> GetVersionAsync()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("GetVersion"));
        var response = await SendAsync(doc);
        return response?.Descendants("GetVersionResult").FirstOrDefault()?.Value ?? "";
    }

    public async Task<(string version, int envCount)?> GetStatusAsync()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            BuildSoapEnvelope("GetStatus"));
        var response = await SendAsync(doc);
        if (response is null) return null;
        var ver = (string?)response.Descendants("version").FirstOrDefault() ?? "";
        var env = (string?)response.Descendants("environmentCount").FirstOrDefault() ?? "0";
        return (ver, int.TryParse(env, out var n) ? n : 0);
    }

    // ── Internals ────────────────────────────────────────────────

    private XElement BuildSoapEnvelope(string method, params XElement?[] body)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
        XNamespace tns = "http://lo.revival/";

        return new XElement(soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", xsd.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "tns", tns.NamespaceName),
            new XElement(soap + "Body",
                new XElement(tns + method, body.Where(b => b != null).ToArray())));
    }

    private XElement BuildScript(ScriptExecution script)
    {
        return new XElement("ScriptExecution",
            new XElement("name", script.Name),
            new XElement("script", script.Script),
            new XElement("arguments",
                script.Arguments.Select(a => a.ToXml("LuaValue")).ToArray()));
    }

    private async Task<XDocument?> SendAsync(XDocument request)
    {
        try
        {
            var content = new StringContent(request.Declaration + request.ToString(), Encoding.UTF8, "text/xml");
            content.Headers.Remove("Content-Type");
            content.Headers.Add("Content-Type", "text/xml; charset=utf-8");
            var response = await _http.PostAsync("", content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("RCC HTTP {Status}: {Body}", (int)response.StatusCode, body);
                return null;
            }
            return XDocument.Parse(body);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "RCC call failed");
            return null;
        }
    }

    private async Task<bool> CallAsync(XDocument request, string method)
    {
        var response = await SendAsync(request);
        if (response is null) return false;
        if (response.Descendants("Fault").Any())
        {
            var fault = response.Descendants("faultstring").FirstOrDefault()?.Value ?? "unknown";
            _log.LogWarning("RCC {Method} SOAP fault: {Fault}", method, fault);
            await _cb.OnSoapFaultAsync(method, fault, null);
            return false;
        }
        return true;
    }

    private async Task<object?> CallAndReturnScalarAsync(XDocument request, string method)
    {
        var response = await SendAsync(request);
        if (response is null) return null;
        var result = response.Descendants(method + "Result").FirstOrDefault();
        if (result is null) return null;
        var txt = result.Value.Trim();
        if (double.TryParse(txt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        if (int.TryParse(txt, out var i)) return i;
        return txt;
    }

    private async Task<List<LuaValue>?> CallAndParseLuaValuesAsync(XDocument request, string method)
    {
        var response = await SendAsync(request);
        if (response is null) return null;
        var list = response.Descendants(method + "Result").Descendants("LuaValue").ToList();
        if (list.Count == 0)
        {
            // The response may be wrapped differently
            list = response.Descendants("LuaValue").ToList();
        }
        var out_ = new List<LuaValue>();
        foreach (var e in list) out_.Add(ParseLuaValue(e));
        return out_;
    }

    private static LuaValue ParseLuaValue(XElement e)
    {
        var typeStr = (string?)e.Element("type") ?? "LUA_TNIL";
        var value = (string?)e.Element("value") ?? "";
        if (!Enum.TryParse<LuaType>(typeStr, out var t)) t = LuaType.LUA_TNIL;
        var sub = e.Element("table")?.Elements("LuaValue").Select(ParseLuaValue).ToList()
                 ?? new List<LuaValue>();
        return new LuaValue(t, value, sub);
    }
}
