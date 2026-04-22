using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Sqlite.LegacyBridge.Ado;

namespace EntityFrameworkCore.Sqlite.Legacy;

public static class SqliteLegacyDbContextOptionsExtensions
{
    /// <summary>
    /// Usa o provider SQLite do EF Core com uma <see cref="DbConnection"/> que delega ao processo bridge net462.
    /// O compilador de consultas LINQ, rastreamento, <c>SaveChanges</c>/<c>SaveChangesAsync</c>, transações e
    /// extensões (<c>ExecuteUpdate</c>/<c>ExecuteDelete</c>, etc.) são os mesmos de <c>UseSqlite</c>; apenas a
    /// execução física de comandos passa pelo bridge.
    /// Prefira configurar em <see cref="DbContext.OnConfiguring"/> (uma conexão por instância do contexto).
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseSqliteLegacy<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<LegacySqliteConnectionOptionsBuilder> configure,
        Action<SqliteDbContextOptionsBuilder>? configureSqlite = null)
        where TContext : DbContext
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        var b = new LegacySqliteConnectionOptionsBuilder();
        configure(b);
        var opts = b.Build();
        var connection = new LegacyBridgeDbConnection(opts);
        return configureSqlite is null
            ? optionsBuilder.UseSqlite(connection)
            : optionsBuilder.UseSqlite(connection, configureSqlite);
    }

    /// <summary>Atalho equivalente a <c>UseSqliteLegacy(o =&gt; o.DatabasePath(path).Password(password))</c>.</summary>
    public static DbContextOptionsBuilder<TContext> UseSqliteLegacy<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string databasePath,
        string password,
        string? hostExecutablePath = null,
        Action<SqliteDbContextOptionsBuilder>? configureSqlite = null)
        where TContext : DbContext =>
        optionsBuilder.UseSqliteLegacy(o =>
        {
            o.DatabasePath(databasePath);
            o.Password(password);
            if (hostExecutablePath != null)
                o.HostExecutablePath(hostExecutablePath);
        }, configureSqlite);

    /// <inheritdoc cref="UseSqliteLegacy{TContext}(DbContextOptionsBuilder{TContext}, Action{LegacySqliteConnectionOptionsBuilder})" />
    public static DbContextOptionsBuilder UseSqliteLegacy(
        this DbContextOptionsBuilder optionsBuilder,
        Action<LegacySqliteConnectionOptionsBuilder> configure,
        Action<SqliteDbContextOptionsBuilder>? configureSqlite = null)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        var b = new LegacySqliteConnectionOptionsBuilder();
        configure(b);
        var opts = b.Build();
        var connection = new LegacyBridgeDbConnection(opts);
        return configureSqlite is null
            ? optionsBuilder.UseSqlite(connection)
            : optionsBuilder.UseSqlite(connection, configureSqlite);
    }

    /// <inheritdoc cref="UseSqliteLegacy{TContext}(DbContextOptionsBuilder{TContext}, string, string, string?)" />
    public static DbContextOptionsBuilder UseSqliteLegacy(
        this DbContextOptionsBuilder optionsBuilder,
        string databasePath,
        string password,
        string? hostExecutablePath = null,
        Action<SqliteDbContextOptionsBuilder>? configureSqlite = null) =>
        optionsBuilder.UseSqliteLegacy(o =>
        {
            o.DatabasePath(databasePath);
            o.Password(password);
            if (hostExecutablePath != null)
                o.HostExecutablePath(hostExecutablePath);
        }, configureSqlite);
}
