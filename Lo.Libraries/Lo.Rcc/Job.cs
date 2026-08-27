namespace Lo.Rcc;

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
