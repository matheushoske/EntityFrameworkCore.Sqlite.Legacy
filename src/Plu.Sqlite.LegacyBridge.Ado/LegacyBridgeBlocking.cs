using System.Threading;
using System.Threading.Tasks;

namespace Sqlite.LegacyBridge.Ado;

/// <summary>
/// Executa trabalho async de forma síncrona. Com <see cref="SynchronizationContext"/> (STA/UI/COM), delega a
/// <see cref="Task.Run"/> para evitar deadlock; em thread MTA sem contexto, usa <c>GetAwaiter().GetResult()</c>
/// direto para não aninhar <c>Task.Run</c> (esgotamento do pool / bloqueios profundos).
/// </summary>
internal static class LegacyBridgeBlocking
{
    /// <summary>
    /// Evita deadlock do thread pool quando <paramref name="work"/> bloqueia em I/O nomeado (Connect) e o chamador
    /// também é worker do pool: <see cref="Thread"/> dedicada não conta para o pool.
    /// </summary>
    internal static T RunOnDedicatedThread<T>(Func<Task<T>> work)
    {
        T? result = default;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                result = work().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true,
            Name = "PluLegacyBridgeOpen",
        };
        t.Start();
        t.Join();
        if (error != null)
            throw error;
        return result!;
    }

    internal static T RunOnDedicatedThread<T>(Func<T> work)
    {
        T? result = default;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true,
            Name = "PluLegacyBridgeOpen",
        };
        t.Start();
        t.Join();
        if (error != null)
            throw error;
        return result!;
    }

    internal static T Run<T>(Func<Task<T>> work)
    {
        if (SynchronizationContext.Current != null)
            return Task.Run(() => work().ConfigureAwait(false).GetAwaiter().GetResult()).GetAwaiter().GetResult();
        return work().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    internal static void Run(Func<Task> work)
    {
        if (SynchronizationContext.Current != null)
            Task.Run(() => work().ConfigureAwait(false).GetAwaiter().GetResult()).GetAwaiter().GetResult();
        else
            work().ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
