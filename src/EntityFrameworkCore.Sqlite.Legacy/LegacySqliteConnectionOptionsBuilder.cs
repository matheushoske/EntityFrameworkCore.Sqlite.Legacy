using Sqlite.LegacyBridge.Ado;

namespace EntityFrameworkCore.Sqlite.Legacy;

public sealed class LegacySqliteConnectionOptionsBuilder
{
    private string _databasePath = "";
    private string _password = "";
    private string? _hostExecutablePath;
    private bool _pooling = true;
    private int _maxPoolSize = 4;
    private TimeSpan _poolIdleTimeout = TimeSpan.FromMinutes(5);

    public LegacySqliteConnectionOptionsBuilder DatabasePath(string path)
    {
        _databasePath = path;
        return this;
    }

    public LegacySqliteConnectionOptionsBuilder Password(string password)
    {
        _password = password;
        return this;
    }

    public LegacySqliteConnectionOptionsBuilder HostExecutablePath(string? path)
    {
        _hostExecutablePath = path;
        return this;
    }

    public LegacySqliteConnectionOptionsBuilder Pooling(bool enabled)
    {
        _pooling = enabled;
        return this;
    }

    public LegacySqliteConnectionOptionsBuilder MaxPoolSize(int maxPoolSize)
    {
        if (maxPoolSize < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPoolSize), "MaxPoolSize deve ser maior que zero.");
        _maxPoolSize = maxPoolSize;
        return this;
    }

    public LegacySqliteConnectionOptionsBuilder PoolIdleTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "PoolIdleTimeout deve ser maior que zero.");
        _poolIdleTimeout = timeout;
        return this;
    }

    internal LegacySqliteConnectionOptions Build() =>
        new()
        {
            DatabasePath = _databasePath,
            Password = _password,
            HostExecutablePath = _hostExecutablePath,
            Pooling = _pooling,
            MaxPoolSize = _maxPoolSize,
            PoolIdleTimeout = _poolIdleTimeout
        };
}
