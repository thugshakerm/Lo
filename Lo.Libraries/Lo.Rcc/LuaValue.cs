using System.Xml.Linq;

namespace Lo.Rcc;

public class LuaValue
{
    public LuaType Type { get; }
    public string Value { get; }

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

    public static LuaValue FromTable(List<LuaValue> values) => new(LuaType.LUA_TTABLE, "", values);

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
