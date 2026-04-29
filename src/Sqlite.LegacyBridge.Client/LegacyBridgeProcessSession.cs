using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Sqlite.LegacyBridge.Client;

/// <summary>Inicia o host net462, conecta o pipe e expõe <see cref="LegacyBridgeSession"/>.</summary>
public sealed class LegacyBridgeProcessSession : IDisposable
{
    private readonly Process _host;
    public LegacyBridgeSession Session { get; }
    public bool IsAlive
    {
        get
        {
            try { return !_host.HasExited; }
            catch { return false; }
        }
    }

    private LegacyBridgeProcessSession(Process host, LegacyBridgeSession session)
    {
        _host = host;
        Session = session;
    }

    /// <summary>Abre o bridge de forma totalmente síncrona na thread atual (usar com <see cref="LegacyBridgeBlocking.RunOnDedicatedThread{T}(Func{T})"/>).</summary>
    public static LegacyBridgeProcessSession StartBlocking(
        string hostExecutablePath,
        string databasePath,
        string password,
        string? pipeName = null)
    {
        pipeName ??= "PluSqliteLegacy_" + Guid.NewGuid().ToString("N");
        Debug.WriteLine($"[LegacyBridge] StartBlocking pipe={pipeName} db={databasePath} (antes Process.Start)");
        var host = LegacyBridgeHostLauncher.Start(hostExecutablePath, pipeName, databasePath, password);
        Debug.WriteLine($"[LegacyBridge] host pid={host.Id} (após Process.Start)");
        try
        {
            try { host.Refresh(); } catch { /* ignore */ }
            if (host.HasExited)
                throw new InvalidOperationException(
                    $"Sqlite.LegacyBridge.Host terminou antes da ligação ao pipe (exit {host.ExitCode}). Verifique legacy\\ e o caminho da base.");
            Debug.WriteLine("[LegacyBridge] conectando ao pipe…");
            var session = LegacyBridgeSession.ConnectBlocking(pipeName);
            Debug.WriteLine("[LegacyBridge] pipe conectado; ping…");
            var ping = Task.Run(() => session.PingAsync().ConfigureAwait(false).GetAwaiter().GetResult())
                .GetAwaiter()
                .GetResult();
            if (!ping.Ok)
                throw new InvalidOperationException(ping.Error ?? "ping falhou");
            return new LegacyBridgeProcessSession(host, session);
        }
        catch
        {
            TryKillHost(host);
            throw;
        }
    }

    public static async Task<LegacyBridgeProcessSession> StartAsync(
        string hostExecutablePath,
        string databasePath,
        string password,
        string? pipeName = null,
        CancellationToken cancellationToken = default)
    {
        pipeName ??= "PluSqliteLegacy_" + Guid.NewGuid().ToString("N");
        Debug.WriteLine($"[LegacyBridge] StartAsync pipe={pipeName} db={databasePath} (antes Process.Start)");
        var host = LegacyBridgeHostLauncher.Start(hostExecutablePath, pipeName, databasePath, password);
        Debug.WriteLine($"[LegacyBridge] host pid={host.Id} (após Process.Start)");
        try
        {
            try { host.Refresh(); } catch { /* ignore */ }
            if (host.HasExited)
                throw new InvalidOperationException(
                    $"Sqlite.LegacyBridge.Host terminou antes da ligação ao pipe (exit {host.ExitCode}). Verifique legacy\\ e o caminho da base.");
            Debug.WriteLine("[LegacyBridge] conectando ao pipe…");
            var session = await LegacyBridgeSession.ConnectAsync(pipeName, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine("[LegacyBridge] pipe conectado; ping…");
            var ping = await session.PingAsync(cancellationToken).ConfigureAwait(false);
            if (!ping.Ok)
                throw new InvalidOperationException(ping.Error ?? "ping falhou");
            return new LegacyBridgeProcessSession(host, session);
        }
        catch
        {
            TryKillHost(host);
            throw;
        }
    }

    private static void TryKillHost(Process host)
    {
        try
        {
#if NETSTANDARD2_0
            if (!host.HasExited)
                host.Kill();
#else
            host.Kill(entireProcessTree: true);
#endif
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        try { Session.Dispose(); } catch { /* ignore */ }
        try
        {
            if (!_host.HasExited)
            {
#if NETSTANDARD2_0
                _host.Kill();
#else
                _host.Kill(entireProcessTree: true);
#endif
            }
        }
        catch { /* ignore */ }

        try { _host.Dispose(); } catch { /* ignore */ }
    }
}
