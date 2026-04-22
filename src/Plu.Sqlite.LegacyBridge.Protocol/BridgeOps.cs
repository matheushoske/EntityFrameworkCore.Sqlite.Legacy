namespace Sqlite.LegacyBridge.Protocol;

public static class BridgeOps
{
    public const string Ping = "ping";
    public const string Open = "open";
    public const string Close = "close";
    public const string ExecuteReader = "executeReader";
    public const string ExecuteNonQuery = "executeNonQuery";
    public const string ExecuteScalar = "executeScalar";
    public const string BeginTransaction = "beginTransaction";
    public const string Commit = "commit";
    public const string Rollback = "rollback";
}
