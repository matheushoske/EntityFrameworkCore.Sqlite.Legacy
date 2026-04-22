using System.Diagnostics;
using System.Text;

namespace Sqlite.LegacyBridge.Client;

public sealed class LegacyBridgeHostLauncher
{
    public static Process Start(string hostExecutablePath, string pipeName, string databasePath, string password)
    {
        if (!File.Exists(hostExecutablePath))
            throw new FileNotFoundException("Host bridge não encontrado.", hostExecutablePath);

        var hostDir = Path.GetDirectoryName(hostExecutablePath);
        if (string.IsNullOrEmpty(hostDir))
            throw new InvalidOperationException("Caminho do host inválido.");

        var psi = new ProcessStartInfo
        {
            FileName = hostExecutablePath,
            WorkingDirectory = hostDir,
            UseShellExecute = false,
            RedirectStandardError = false,
            RedirectStandardOutput = false,
            CreateNoWindow = false,
        };

#if NETSTANDARD2_0
        psi.Arguments = BuildProcessArguments(pipeName, databasePath);
#else
        psi.ArgumentList.Add("--pipe");
        psi.ArgumentList.Add(pipeName);
        psi.ArgumentList.Add("--database");
        psi.ArgumentList.Add(databasePath);
#endif
        psi.Environment["PLU_SQLITE_PASSWORD"] = password;

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!p.Start())
            throw new InvalidOperationException("Falha ao iniciar o processo bridge.");
        return p;
    }

#if NETSTANDARD2_0
    private static string BuildProcessArguments(string pipeName, string databasePath)
    {
        var sb = new StringBuilder();
        sb.Append("--pipe ");
        AppendQuoted(sb, pipeName);
        sb.Append(" --database ");
        AppendQuoted(sb, databasePath);
        return sb.ToString();
    }

    private static void AppendQuoted(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var ch in value)
        {
            if (ch == '\\' || ch == '"')
                sb.Append('\\');
            sb.Append(ch);
        }

        sb.Append('"');
    }
#endif

    /// <summary>Procura Sqlite.LegacyBridge.Host.exe em legacy/ relativo ao assembly ou base directory.</summary>
    public static string ResolveHostPath(string? overridePath = null)
    {
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            return Path.GetFullPath(overridePath);

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "legacy", "Sqlite.LegacyBridge.Host.exe"),
            Path.Combine(baseDir, "Sqlite.LegacyBridge.Host.exe"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return Path.GetFullPath(c);
        }

        throw new FileNotFoundException(
            "Sqlite.LegacyBridge.Host.exe não encontrado. Copie a pasta legacy/ para a saída do aplicativo.");
    }
}
