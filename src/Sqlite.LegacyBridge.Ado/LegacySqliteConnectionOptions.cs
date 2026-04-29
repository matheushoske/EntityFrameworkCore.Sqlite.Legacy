namespace Sqlite.LegacyBridge.Ado;

public sealed class LegacySqliteConnectionOptions
{
    public string DatabasePath { get; set; } = "";
    public string Password { get; set; } = "";
    public string? HostExecutablePath { get; set; }
    public bool Pooling { get; set; } = true;
    public int MaxPoolSize { get; set; } = 4;
    public TimeSpan PoolIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
