using System.Xml.Linq;

namespace Lo.Rcc;

/// <summary>
/// A typed Lua value passed across the RCCService SOAP boundary.
/// WSDL defines a LuaValue as {type: LuaType, value: string, table: LuaValue[]}.
///
/// We use this as the in-process representation; conversion to/from
/// the SOAP wire format happens in RccClient.
/// </summary>
public class LuaValue
{
    public LuaType Type { get; }
    public string Value { get; }

    /// <summary>
    /// Sub-values (for LUA_TTABLE). Property name kept as "Table"
    /// to mirror the WSDL element name and Finobe's RbxArbiter. Note
    /// that the static factory for building a table LuaValue is
    /// called <see cref="FromTable"/> to avoid the name collision.
    /// </summary>
    public List<LuaValue> Table { get; }

    public LuaValue(LuaType type, string value, List<LuaValue>? table = null)
    {
        Type = type;
        Value = value;
        Table = table ?? new List<LuaValue>();
    }

    public static LuaValue Nil() => new(LuaType.LUA_TNIL, "");
    public static LuaValue Bool(bool v) => new(LuaType.LUA_TBOOLEAN, v ? "true" : "false");
    public static LuaValue Int(long v) => new(LuaType.LUA_TNUMBER, v.ToString());
    public static LuaValue Num(double v) => new(LuaType.LUA_TNUMBER, v.ToString());
    public static LuaValue Str(string v) => new(LuaType.LUA_TSTRING, v);

    /// <summary>
    /// Build a LUA_TTABLE LuaValue from a list of sub-values.
    /// (Renamed from <c>Table()</c> to avoid colliding with the
    /// <see cref="Table"/> property.)
    /// </summary>
    public static LuaValue FromTable(List<LuaValue> values) => new(LuaType.LUA_TTABLE, "", values);

    /// <summary>
    /// Serialize to the WSDL wire format. Used for sending arguments.
    /// </summary>
    public XElement ToXml(string name)
    {
        XElement e = new(name);
        e.Add(new XElement("type", Type.ToString()));
        e.Add(new XElement("value", Value));
        XElement tbl = new("table");
        foreach (var sub in Table)
        {
            tbl.Add(sub.ToXml("LuaValue"));
        }
        e.Add(tbl);
        return e;
    }
}
