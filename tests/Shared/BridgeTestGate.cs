namespace Plu.Testing;

/// <summary>
/// Serializa testes que disparam o host net462 + pipe (evita corrida e pipe quebrado).
/// </summary>
public static class BridgeTestGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task WithExclusiveBridgeAsync(Func<Task> action)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static void WithExclusiveBridge(Action action)
    {
        Gate.Wait();
        try
        {
            action();
        }
        finally
        {
            Gate.Release();
        }
    }
}
