using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Sqlite.LegacyBridge.Client;

namespace Sqlite.LegacyBridge.Ado;

public sealed class LegacyBridgeDbConnection : DbConnection
{
    private readonly LegacySqliteConnectionOptions _options;
    private LegacyBridgeProcessSession? _process;
    private ConnectionState _state = ConnectionState.Closed;
    private LegacyBridgeDbTransaction? _transaction;

    public LegacyBridgeDbConnection(LegacySqliteConnectionOptions options)
    {
        _options = options;
        // Mantém a connection string compatível com Microsoft.Data.Sqlite (sem keywords custom).
        ConnectionString = $"Data Source={options.DatabasePath}";
    }

    public LegacyBridgeProcessSession? ProcessSession => _process;

    internal LegacyBridgeSession Session => _process?.Session ?? throw new InvalidOperationException("Conexão fechada.");

    internal LegacyBridgeDbTransaction? CurrentTransaction => _transaction;

    public override string ConnectionString { get; set; }

    public override string Database => "main";

    public override string DataSource => _options.DatabasePath;

    public override string ServerVersion => "legacy-bridge";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) =>
        throw new NotSupportedException();

    public override void Close()
    {
        if (_state == ConnectionState.Closed)
            return;

        _transaction?.Dispose();
        _transaction = null;
        _process?.Dispose();
        _process = null;
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        if (_state == ConnectionState.Open)
            return;

        var host = string.IsNullOrEmpty(_options.HostExecutablePath)
            ? LegacyBridgeHostLauncher.ResolveHostPath()
            : _options.HostExecutablePath!;
        var dbPath = Path.GetFullPath(_options.DatabasePath);
        var sw = Stopwatch.StartNew();
        Debug.WriteLine($"[LegacyBridgeDbConnection] Open begin db={dbPath} host={host}");
        Console.WriteLine($"[LegacyBridgeDbConnection] Open begin db={dbPath}");

        _process = LegacyBridgeBlocking.RunOnDedicatedThread(
            () => LegacyBridgeProcessSession.StartBlocking(host, dbPath, _options.Password));

        sw.Stop();
        Debug.WriteLine($"[LegacyBridgeDbConnection] Open end em {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"[LegacyBridgeDbConnection] Open end em {sw.ElapsedMilliseconds}ms");

        _state = ConnectionState.Open;
    }

    /// <summary>Preferir em código async (e o EF Core usa isto em <c>SaveChangesAsync</c>, queries async, etc.) para evitar bloquear a thread com <c>Open()</c>.</summary>
    /// <remarks><c>Open</c> arranca o bridge numa thread dedicada para evitar deadlock do thread pool com <c>NamedPipeClientStream.Connect</c> e <c>SynchronizationContext</c>.</remarks>
    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        if (_state == ConnectionState.Open)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        var host = string.IsNullOrEmpty(_options.HostExecutablePath)
            ? LegacyBridgeHostLauncher.ResolveHostPath()
            : _options.HostExecutablePath!;

        _process = await LegacyBridgeProcessSession.StartAsync(
            host,
            Path.GetFullPath(_options.DatabasePath),
            _options.Password,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _state = ConnectionState.Open;
    }

    internal void EnsureOpen()
    {
        if (_state != ConnectionState.Open)
            throw new InvalidOperationException("Abra a conexão antes.");
    }

    internal void ClearTransaction() => _transaction = null;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        EnsureOpen();
        if (_transaction != null)
            throw new InvalidOperationException("Transação já ativa.");

        var id = LegacyBridgeBlocking.Run(() => Session.BeginTransactionAsync());
        _transaction = new LegacyBridgeDbTransaction(this, id);
        return _transaction;
    }

#if !NETSTANDARD2_0
    protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        EnsureOpen();
        if (_transaction != null)
            throw new InvalidOperationException("Transação já ativa.");

        var id = await Session.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        _transaction = new LegacyBridgeDbTransaction(this, id);
        return _transaction;
    }
#endif

    protected override DbCommand CreateDbCommand() => new LegacyBridgeDbCommand(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Close();
        base.Dispose(disposing);
    }
}
