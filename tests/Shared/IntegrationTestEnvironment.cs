using Sqlite.LegacyBridge.Client;

namespace Plu.Testing;

public static class IntegrationTestEnvironment
{
    private const string DevPasswordFallback = "R@enil2015#";

    public static string GetPassword() =>
        Environment.GetEnvironmentVariable("PLU_SQLITE_PASSWORD") ?? DevPasswordFallback;

    public static string GetDatabasePath()
    {
        var env = Environment.GetEnvironmentVariable("PLU_TEST_DB");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var full = Path.GetFullPath(env.Trim());
            if (!File.Exists(full))
                throw new InvalidOperationException(
                    $"PLU_TEST_DB está definido mas o arquivo não existe: {full}");
            return full;
        }

        var root = FindRepoRoot();
        var p = Path.Combine(root, "plu.db3");
        if (!File.Exists(p))
            throw new InvalidOperationException(
                "Arquivo plu.db3 não encontrado na raiz do repositório. Defina PLU_TEST_DB ou adicione plu.db3.");

        return p;
    }

    public static string GetHostExecutablePath() =>
        LegacyBridgeHostLauncher.ResolveHostPath();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "plu.db3")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Não foi possível localizar a raiz do repositório (plu.db3). Use PLU_TEST_DB.");
    }
}
