using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Sqlite.LegacyBridge.Client;

namespace Sqlite.LegacyBridge.Ado;

internal static class LegacyBridgeSessionPool
{
    private sealed class PooledSession
    {
        public PooledSession(LegacyBridgeProcessSession session)
        {
            Session = session;
            LastReturnedUtc = DateTime.UtcNow;
        }

        public LegacyBridgeProcessSession Session { get; }
        public DateTime LastReturnedUtc { get; set; }
    }

    private static readonly ConcurrentDictionary<string, ConcurrentQueue<PooledSession>> Pools = new();

    public static LegacyBridgeProcessSession Rent(
        string hostExecutablePath,
        string databasePath,
        string password,
        int maxPoolSize,
        TimeSpan idleTimeout)
    {
        var key = CreateKey(hostExecutablePath, databasePath, password);
        var queue = Pools.GetOrAdd(key, _ => new ConcurrentQueue<PooledSession>());
        while (queue.TryDequeue(out var pooled))
        {
            if (IsReusable(pooled, idleTimeout))
                return pooled.Session;

            pooled.Session.Dispose();
        }

        return LegacyBridgeBlocking.RunOnDedicatedThread(
            () => LegacyBridgeProcessSession.StartBlocking(hostExecutablePath, databasePath, password));
    }

    public static async Task<LegacyBridgeProcessSession> RentAsync(
        string hostExecutablePath,
        string databasePath,
        string password,
        int maxPoolSize,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken)
    {
        var key = CreateKey(hostExecutablePath, databasePath, password);
        var queue = Pools.GetOrAdd(key, _ => new ConcurrentQueue<PooledSession>());
        while (queue.TryDequeue(out var pooled))
        {
            if (IsReusable(pooled, idleTimeout))
                return pooled.Session;

            pooled.Session.Dispose();
        }

        return await LegacyBridgeProcessSession.StartAsync(
            hostExecutablePath,
            databasePath,
            password,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static void Return(
        string hostExecutablePath,
        string databasePath,
        string password,
        LegacyBridgeProcessSession session,
        int maxPoolSize)
    {
        if (!session.IsAlive)
        {
            session.Dispose();
            return;
        }

        var key = CreateKey(hostExecutablePath, databasePath, password);
        var queue = Pools.GetOrAdd(key, _ => new ConcurrentQueue<PooledSession>());
        if (queue.Count >= maxPoolSize)
        {
            session.Dispose();
            return;
        }

        queue.Enqueue(new PooledSession(session));
    }

    public static void ClearAll()
    {
        foreach (var pair in Pools)
        {
            while (pair.Value.TryDequeue(out var pooled))
                pooled.Session.Dispose();
        }

        Pools.Clear();
    }

    private static bool IsReusable(PooledSession pooled, TimeSpan idleTimeout)
    {
        if (!pooled.Session.IsAlive)
            return false;
        return DateTime.UtcNow - pooled.LastReturnedUtc <= idleTimeout;
    }

    private static string CreateKey(string hostExecutablePath, string databasePath, string password)
    {
        var normalized = string.Join(
            "|",
            Path.GetFullPath(hostExecutablePath).ToUpperInvariant(),
            Path.GetFullPath(databasePath).ToUpperInvariant(),
            ComputeSha256(password));
        return normalized;
    }

    private static string ComputeSha256(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
