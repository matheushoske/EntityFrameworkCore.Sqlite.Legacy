using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Sqlite.LegacyBridge.Protocol;
using Sqlite.LegacyBridge.Protocol.Payloads;

namespace Sqlite.LegacyBridge.Client;

/// <summary>Uma sessão = um pipe conectado a um host já em execução (uma conexão SQLite no processo net462).</summary>
public sealed class LegacyBridgeSession : IDisposable
{
    private const string PipePrefix = @"\\.\pipe\";

    private readonly NamedPipeClientStream _pipe;
    private long _nextId;
    private bool _disposed;

    public LegacyBridgeSession(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
    }

    private static void ReadExact(Stream stream, byte[] buffer, int length)
    {
        var offset = 0;
        while (offset < length)
        {
            var n = stream.Read(buffer, offset, length - offset);
            if (n == 0)
                throw new IOException("Handshake bridge: fluxo fechado antes dos bytes esperados.");
            offset += n;
        }
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    private static string? ReadUtf8Line(Stream stream)
    {
        using var acc = new MemoryStream();
        var one = new byte[1];
        while (true)
        {
            var n = stream.Read(one, 0, 1);
            if (n == 0)
                return acc.Length == 0 ? null : Encoding.UTF8.GetString(acc.ToArray());
            if (one[0] == (byte)'\n')
                return Encoding.UTF8.GetString(acc.ToArray());
            if (one[0] != (byte)'\r')
                acc.WriteByte(one[0]);
        }
    }

    private static void WriteUtf8Line(Stream stream, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    /// <summary>Caminho síncrono para <see cref="LegacyBridgeDbConnection.Open"/> numa thread dedicada (sem continuations no thread pool).</summary>
    internal static LegacyBridgeSession ConnectBlocking(string pipeName)
    {
        var shortName = pipeName.StartsWith(PipePrefix, StringComparison.OrdinalIgnoreCase)
            ? pipeName.Substring(PipePrefix.Length)
            : pipeName;

        var pipe = new NamedPipeClientStream(
            ".",
            shortName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        pipe.Connect(30_000);
        try
        {
            pipe.ReadMode = PipeTransmissionMode.Byte;
        }
        catch (Exception)
        {
        }

        const string ready = "PLU_LEGACY_BRIDGE_READY";
        var readyBytes = Encoding.UTF8.GetBytes(ready + "\n");
        var readyBuf = new byte[readyBytes.Length];
        ReadExact(pipe, readyBuf, readyBuf.Length);
        if (!BytesEqual(readyBuf, readyBytes))
            throw new IOException("Handshake bridge: linha READY inválida.");

        var ack = Encoding.UTF8.GetBytes("PLU_LEGACY_CLIENT_ACK\n");
        pipe.Write(ack, 0, ack.Length);
        pipe.Flush();

        return new LegacyBridgeSession(pipe);
    }

    public static async Task<LegacyBridgeSession> ConnectAsync(string pipeName, CancellationToken cancellationToken = default)
    {
        var shortName = pipeName.StartsWith(PipePrefix, StringComparison.OrdinalIgnoreCase)
            ? pipeName.Substring(PipePrefix.Length)
            : pipeName;

        var pipe = new NamedPipeClientStream(
            ".",
            shortName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        cancellationToken.ThrowIfCancellationRequested();
        pipe.Connect(30_000);
        try
        {
            pipe.ReadMode = PipeTransmissionMode.Byte;
        }
        catch (Exception)
        {
        }

        const string ready = "PLU_LEGACY_BRIDGE_READY";
        var readyBytes = Encoding.UTF8.GetBytes(ready + "\n");
        var readyBuf = new byte[readyBytes.Length];
        await Task.Run(
            () =>
            {
                ReadExact(pipe, readyBuf, readyBuf.Length);
                if (!BytesEqual(readyBuf, readyBytes))
                    throw new IOException("Handshake bridge: linha READY inválida.");
            },
            cancellationToken).ConfigureAwait(false);

        var ack = Encoding.UTF8.GetBytes("PLU_LEGACY_CLIENT_ACK\n");
        pipe.Write(ack, 0, ack.Length);
        pipe.Flush();

        return new LegacyBridgeSession(pipe);
    }

    public async Task<BridgeResponse> SendAsync(BridgeEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LegacyBridgeSession));

        var line = JsonSerializer.Serialize(envelope, BridgeJson.Options);
        await Task.Run(() => WriteUtf8Line(_pipe, line), cancellationToken).ConfigureAwait(false);
        var respLine = await Task.Run(() => ReadUtf8Line(_pipe), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(respLine))
            throw new IOException("Bridge encerrou a conexão sem resposta.");
        return JsonSerializer.Deserialize<BridgeResponse>(respLine!, BridgeJson.Options)
               ?? throw new JsonException("Resposta inválida do bridge.");
    }

    public Task<BridgeResponse> PingAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new BridgeEnvelope { Id = Interlocked.Increment(ref _nextId), Op = BridgeOps.Ping }, cancellationToken);

    public Task<BridgeResponse> ExecuteReaderAsync(string sql, SqlParameterDto[]? parameters = null, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new SqlPayload { Sql = sql, Parameters = parameters }, BridgeJson.Options);
        return SendAsync(new BridgeEnvelope
        {
            Id = Interlocked.Increment(ref _nextId),
            Op = BridgeOps.ExecuteReader,
            Payload = payload
        }, cancellationToken);
    }

    public Task<BridgeResponse> ExecuteNonQueryAsync(string sql, SqlParameterDto[]? parameters = null, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new SqlPayload { Sql = sql, Parameters = parameters }, BridgeJson.Options);
        return SendAsync(new BridgeEnvelope
        {
            Id = Interlocked.Increment(ref _nextId),
            Op = BridgeOps.ExecuteNonQuery,
            Payload = payload
        }, cancellationToken);
    }

    public Task<BridgeResponse> ExecuteScalarAsync(string sql, SqlParameterDto[]? parameters = null, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new SqlPayload { Sql = sql, Parameters = parameters }, BridgeJson.Options);
        return SendAsync(new BridgeEnvelope
        {
            Id = Interlocked.Increment(ref _nextId),
            Op = BridgeOps.ExecuteScalar,
            Payload = payload
        }, cancellationToken);
    }

    public async Task<int> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var r = await SendAsync(new BridgeEnvelope { Id = Interlocked.Increment(ref _nextId), Op = BridgeOps.BeginTransaction }, cancellationToken).ConfigureAwait(false);
        if (!r.Ok || r.Result?.TransactionId is not int tid)
            throw new InvalidOperationException(r.Error ?? "beginTransaction falhou");
        return tid;
    }

    public async Task CommitAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new TransactionPayload { TransactionId = transactionId }, BridgeJson.Options);
        var r = await SendAsync(new BridgeEnvelope
        {
            Id = Interlocked.Increment(ref _nextId),
            Op = BridgeOps.Commit,
            Payload = payload
        }, cancellationToken).ConfigureAwait(false);
        if (!r.Ok)
            throw new InvalidOperationException(r.Error ?? "commit falhou");
    }

    public async Task RollbackAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(new TransactionPayload { TransactionId = transactionId }, BridgeJson.Options);
        var r = await SendAsync(new BridgeEnvelope
        {
            Id = Interlocked.Increment(ref _nextId),
            Op = BridgeOps.Rollback,
            Payload = payload
        }, cancellationToken).ConfigureAwait(false);
        if (!r.Ok)
            throw new InvalidOperationException(r.Error ?? "rollback falhou");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _pipe.Dispose(); } catch { /* ignore */ }
    }
}
