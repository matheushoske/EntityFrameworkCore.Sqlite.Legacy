using Sqlite.LegacyBridge.Ado;

namespace EntityFrameworkCore.Sqlite.Legacy;

public sealed class LegacySqliteConnectionOptionsBuilder
{
    private string _databasePath = "";
    private string _password = "";
    private string? _hostExecutablePath;

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

    internal LegacySqliteConnectionOptions Build() =>
        new()
        {
            DatabasePath = _databasePath,
            Password = _password,
            HostExecutablePath = _hostExecutablePath
        };
}
