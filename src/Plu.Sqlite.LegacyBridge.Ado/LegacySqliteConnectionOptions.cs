namespace Sqlite.LegacyBridge.Ado;

public sealed class LegacySqliteConnectionOptions
{
    public string DatabasePath { get; set; } = "";
    public string Password { get; set; } = "";
    public string? HostExecutablePath { get; set; }
}
