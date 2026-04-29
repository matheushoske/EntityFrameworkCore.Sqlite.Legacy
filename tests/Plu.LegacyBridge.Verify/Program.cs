using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using EntityFrameworkCore.Sqlite.Legacy;
using Sqlite.LegacyBridge.Ado;
using Sqlite.LegacyBridge.Client;
using Plu.Testing;

namespace Plu.LegacyBridge.Verify;

internal static class Program
{
    private static int Fail(string msg)
    {
        Console.Error.WriteLine(msg);
        return 1;
    }

    public static async Task<int> Main()
    {
        try
        {
            Console.WriteLine("0) Unit: SetupBridgeHost baseDir default path…");
            VerifySetupBridgeHostBaseDirectoryDefault();

            var db = IntegrationTestEnvironment.GetDatabasePath();
            var pwd = IntegrationTestEnvironment.GetPassword();
            var host = IntegrationTestEnvironment.GetHostExecutablePath();

            Console.WriteLine("1) Ping…");
            using (var session = await LegacyBridgeProcessSession.StartAsync(host, db, pwd))
            {
                var pong = await session.Session.PingAsync();
                if (!pong.Ok || pong.Result?.Kind != "pong")
                    return Fail("Ping falhou: " + pong.Error);
            }

            Console.WriteLine("2) executeReader versao…");
            using (var session = await LegacyBridgeProcessSession.StartAsync(host, db, pwd))
            {
                var r = await session.Session.ExecuteReaderAsync("SELECT * FROM versao LIMIT 1");
                if (!r.Ok || r.Result?.Columns is null || !r.Result.Columns.Contains("EXECUCAO"))
                    return Fail("executeReader falhou: " + r.Error);
            }

            Console.WriteLine("3) ADO ExecuteScalar…");
            var opts = new LegacySqliteConnectionOptions
            {
                DatabasePath = db,
                Password = pwd,
                HostExecutablePath = host
            };
            using (var conn = new LegacyBridgeDbConnection(opts))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
                var n = cmd.ExecuteScalar();
                if (n is null || Convert.ToInt64(n) <= 0)
                    return Fail("ADO scalar inválido.");
            }

            Console.WriteLine("4) EF UseSqliteLegacy…");
            var efOptions = new DbContextOptionsBuilder<VersaoVerifyContext>()
                .UseSqliteLegacy(db, pwd, host)
                .Options;
            using (var ctx = new VersaoVerifyContext(efOptions))
            {
                var row = ctx.Versao.AsNoTracking().FirstOrDefault();
                if (row is null || string.IsNullOrEmpty(row.EXECUCAO))
                    return Fail("EF não leu versao.");
            }

            Console.WriteLine("5) EF LINQ (Where, OrderBy, Include, SaveChanges, transação, ExecuteUpdate/Delete)…");
            var linqErr = await EfLinqFeatureVerify.RunAllAsync(db, pwd, host);
            if (linqErr != null)
                return Fail(linqErr);

            Console.WriteLine("6) Paridade UseSqlite (temp db)…");
            var plain = Path.Combine(Path.GetTempPath(), "plu_verify_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var cs = new SqliteConnectionStringBuilder { DataSource = plain }.ToString();
                await using (var c = new SqliteConnection(cs))
                {
                    await c.OpenAsync();
                    await using var cmd = c.CreateCommand();
                    cmd.CommandText = """
                        CREATE TABLE versao (EXECUCAO TEXT NOT NULL);
                        INSERT INTO versao (EXECUCAO) VALUES ('parity');
                        """;
                    await cmd.ExecuteNonQueryAsync();
                }

                var plainOpts = new DbContextOptionsBuilder<VersaoVerifyContext>().UseSqlite(cs).Options;
                using var ctx2 = new VersaoVerifyContext(plainOpts);
                var one = ctx2.Versao.AsNoTracking().Single();
                if (one.EXECUCAO != "parity")
                    return Fail("Paridade UseSqlite falhou.");
            }
            finally
            {
                try
                {
                    if (File.Exists(plain))
                        File.Delete(plain);
                }
                catch
                {
                    // ignore
                }
            }

            Console.WriteLine("7) Pin EF.Sqlite 10.0.0.0…");
            var asm = typeof(SqliteDbContextOptionsBuilderExtensions).Assembly;
            var ver = new AssemblyName(asm.FullName!).Version;
            if (ver != new Version(10, 0, 0, 0))
                return Fail("Versão EF.Sqlite inesperada: " + ver);

            Console.WriteLine("8) Pool smoke benchmark…");
            var pooledOptions = new DbContextOptionsBuilder<VersaoVerifyContext>()
                .UseSqliteLegacy(db, pwd, host)
                .Options;
            var firstOpen = await MeasureEfFirstRowAsync(pooledOptions);
            var secondOpen = await MeasureEfFirstRowAsync(pooledOptions);
            Console.WriteLine($"POOL_SMOKE_FIRST_OPEN_MS={firstOpen}");
            Console.WriteLine($"POOL_SMOKE_SECOND_OPEN_MS={secondOpen}");
            if (secondOpen > firstOpen)
                Console.WriteLine("POOL_SMOKE_NOTE=second open was not faster in this run; keeping full E2E result as source of truth.");

            Console.WriteLine("OK — todas as verificações passaram.");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail(ex.ToString());
        }
        finally
        {
            SqliteLegacyDbContextOptionsExtensions.ClearBridgeHostPools();
        }
    }

    private static void VerifySetupBridgeHostBaseDirectoryDefault()
    {
        const string envName = "SQLITE_LEGACY_BRIDGE_HOST_EXE_PATH";
        var previousValue = Environment.GetEnvironmentVariable(envName);
        var tempBase = Path.Combine(Path.GetTempPath(), "plu_bridge_unit_" + Guid.NewGuid().ToString("N"));
        try
        {
            var legacyDir = Path.Combine(tempBase, "legacy");
            Directory.CreateDirectory(legacyDir);
            var expectedHost = Path.Combine(legacyDir, "Sqlite.LegacyBridge.Host.exe");
            File.WriteAllBytes(expectedHost, Array.Empty<byte>());

            SqliteLegacyDbContextOptionsExtensions.SetupBridgeHost(tempBase);
            var resolved = LegacyBridgeHostLauncher.ResolveHostPath();
            if (!string.Equals(Path.GetFullPath(expectedHost), resolved, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SetupBridgeHost(baseDir) não configurou o host default no mesmo baseDir.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previousValue);
            try
            {
                if (Directory.Exists(tempBase))
                    Directory.Delete(tempBase, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static async Task<long> MeasureEfFirstRowAsync(DbContextOptions<VersaoVerifyContext> options)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var ctx = new VersaoVerifyContext(options);
        var row = await ctx.Versao.AsNoTracking().FirstOrDefaultAsync();
        if (row is null)
            throw new InvalidOperationException("Pool smoke: versao não retornou linhas.");
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }
}

public sealed class VersaoVerifyContext : DbContext
{
    public VersaoVerifyContext(DbContextOptions<VersaoVerifyContext> options) : base(options) { }

    public DbSet<VersaoVerifyRow> Versao => Set<VersaoVerifyRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VersaoVerifyRow>(e =>
        {
            e.ToTable("versao");
            e.HasNoKey();
        });
    }
}

public sealed class VersaoVerifyRow
{
    public string? EXECUCAO { get; set; }
}
