namespace Lo.Rcc;

/// <summary>
/// The LuaType enum used by the RCCService WSDL.
/// WSDL defines five types; Finobe's RobloxArbiterUtilities only
/// implements the three it actually uses. We keep all five for completeness.
///
/// Source: wiki/rccservice/windows/how2rcc.md
/// </summary>
public enum LuaType
{
    LUA_TNIL,
    LUA_TBOOLEAN,
    LUA_TNUMBER,
    LUA_TSTRING,
    LUA_TTABLE
}
