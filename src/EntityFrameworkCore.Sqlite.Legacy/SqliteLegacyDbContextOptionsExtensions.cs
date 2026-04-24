using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Sqlite.LegacyBridge.Ado;
using System.IO.Compression;
using System.Net.Http;

namespace EntityFrameworkCore.Sqlite.Legacy;

public static class SqliteLegacyDbContextOptionsExtensions
{
    private const string DefaultHostZipUrl =
        "https://github.com/matheushoske/Sqlite.LegacyBridge.Host/releases/latest/download/Sqlite.LegacyBridge.Host.zip";
    private const string DefaultHostPathEnvironmentVariable = "SQLITE_LEGACY_BRIDGE_HOST_EXE_PATH";

    /// <summary>
    /// Garante que o host legado exista em <c>legacy/</c> para execução local.
    /// Se ausente, descarrega e extrai automaticamente o zip oficial.
    /// </summary>
    /// <param name="baseDirectory">
    /// Pasta base da aplicação. Se nulo, usa <see cref="AppContext.BaseDirectory"/>.
    /// </param>
    /// <param name="downloadUrl">
    /// URL opcional do zip do host. Se nula, usa a release pública oficial.
    /// </param>
    public static void SetupBridgeHost(string? baseDirectory = null, string? downloadUrl = null)
    {
        var appBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory!;
        var legacyDirectory = Path.Combine(Path.GetFullPath(appBaseDirectory), "legacy");
        var hostExePath = Path.Combine(legacyDirectory, "Sqlite.LegacyBridge.Host.exe");

        // Keep a process-level default host path aligned with SetupBridgeHost(baseDir).
        // Explicit HostExecutablePath in UseSqliteLegacy still has precedence.
        Environment.SetEnvironmentVariable(DefaultHostPathEnvironmentVariable, hostExePath);

        if (HostExists(legacyDirectory))
            return;

        Directory.CreateDirectory(legacyDirectory);
        var url = string.IsNullOrWhiteSpace(downloadUrl) ? DefaultHostZipUrl : downloadUrl!;
        var tempZip = Path.Combine(Path.GetTempPath(), "sqlite-legacy-host-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            DownloadFile(url, tempZip);
            ExtractZip(tempZip, legacyDirectory);
            if (!HostExists(legacyDirectory))
                throw new InvalidOperationException(
                    "Download concluído, mas Sqlite.LegacyBridge.Host.exe não foi encontrado em legacy/.");
        }
        finally
        {
            try
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
            catch
            {
                // cleanup best-effort
            }
        }
    }

    private static bool HostExists(string legacyDirectory)
    {
        var hostExePath = Path.Combine(legacyDirectory, "Sqlite.LegacyBridge.Host.exe");
        return File.Exists(hostExePath);
    }

    private static void DownloadFile(string url, string outputPath)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
        var bytes = httpClient.GetByteArrayAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
        File.WriteAllBytes(outputPath, bytes);
    }

    private static void ExtractZip(string zipPath, string destinationDirectory)
    {
        using var zipStream = File.OpenRead(zipPath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        var destinationFullPath = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!targetPath.StartsWith(destinationFullPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Entrada inválida no zip do host: " + entry.FullName);

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var parentDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(parentDirectory))
                Directory.CreateDirectory(parentDirectory);

            using var entryStream = entry.Open();
            using var outputStream = File.Create(targetPath);
            entryStream.CopyTo(outputStream);
        }
    }

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
