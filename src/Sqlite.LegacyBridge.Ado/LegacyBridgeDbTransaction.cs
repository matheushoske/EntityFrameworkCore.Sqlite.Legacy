using System.Data;
using System.Data.Common;

namespace Sqlite.LegacyBridge.Ado;

public sealed class LegacyBridgeDbTransaction : DbTransaction
{
    private readonly LegacyBridgeDbConnection _connection;
    private readonly int _transactionId;
    private bool _completed;

    internal LegacyBridgeDbTransaction(LegacyBridgeDbConnection connection, int transactionId)
    {
        _connection = connection;
        _transactionId = transactionId;
    }

    public override IsolationLevel IsolationLevel => IsolationLevel.Unspecified;

    protected override DbConnection DbConnection => _connection;

    public override void Commit()
    {
        if (_completed)
            throw new ObjectDisposedException(nameof(LegacyBridgeDbTransaction));
        LegacyBridgeBlocking.Run(() => _connection.Session.CommitAsync(_transactionId));
        _completed = true;
        _connection.ClearTransaction();
    }

#if !NETSTANDARD2_0
    public override async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
            throw new ObjectDisposedException(nameof(LegacyBridgeDbTransaction));
        await _connection.Session.CommitAsync(_transactionId, cancellationToken).ConfigureAwait(false);
        _completed = true;
        _connection.ClearTransaction();
    }
#endif

    public override void Rollback()
    {
        if (_completed)
            throw new ObjectDisposedException(nameof(LegacyBridgeDbTransaction));
        LegacyBridgeBlocking.Run(() => _connection.Session.RollbackAsync(_transactionId));
        _completed = true;
        _connection.ClearTransaction();
    }

#if !NETSTANDARD2_0
    public override async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
            throw new ObjectDisposedException(nameof(LegacyBridgeDbTransaction));
        await _connection.Session.RollbackAsync(_transactionId, cancellationToken).ConfigureAwait(false);
        _completed = true;
        _connection.ClearTransaction();
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_completed)
        {
            try { Rollback(); }
            catch { /* ignore */ }
        }

        base.Dispose(disposing);
    }
}
