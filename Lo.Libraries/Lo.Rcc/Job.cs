namespace Lo.Rcc;

/// <summary>
/// A Job description passed to OpenJob / BatchJob.
/// WSDL shape: {id: string, expirationInSeconds: double, category: int, cores: double}
/// </summary>
public class Job
{
    public string Id { get; }
    public double ExpirationInSeconds { get; }
    public int Category { get; }
    public double Cores { get; }

    public Job(string id, double expirationInSeconds = 600.0, int category = 0, double cores = 1.0)
    {
        Id = id;
        ExpirationInSeconds = expirationInSeconds;
        Category = category;
        Cores = cores;
    }
}

/// <summary>
/// A script execution passed to OpenJob / BatchJob / Execute.
/// WSDL shape: {name: string, script: string, arguments: LuaValue[]}
/// </summary>
public class ScriptExecution
{
    public string Name { get; }
    public string Script { get; }
    public List<LuaValue> Arguments { get; }

    public ScriptExecution(string name, string script, List<LuaValue>? arguments = null)
    {
        Name = name;
        Script = script;
        Arguments = arguments ?? new List<LuaValue>();
    }
}
