using System.Data;
using System.Data.Common;
using System.Text.Json;
using Sqlite.LegacyBridge.Protocol.Payloads;

namespace Sqlite.LegacyBridge.Ado;

public sealed class LegacyBridgeDbCommand : DbCommand
{
    private readonly LegacyBridgeDbConnection _connection;
    private readonly LegacyBridgeParameterCollection _parameters = new();
    private LegacyBridgeDbDataReader? _activeReader;

    internal LegacyBridgeDbCommand(LegacyBridgeDbConnection connection)
    {
        _connection = connection;
    }

    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => _connection;
        set => throw new NotSupportedException();
    }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _connection.CurrentTransaction;
        set
        {
            // EF Core atribui o DbTransaction no comando antes da execução.
            // No bridge, a transação é mantida na conexão/sessão; aqui apenas validamos compatibilidade.
            if (value is null || ReferenceEquals(value, _connection.CurrentTransaction))
                return;
            if (value is LegacyBridgeDbTransaction)
                return;
            throw new NotSupportedException("Somente LegacyBridgeDbTransaction é suportada.");
        }
    }

    public new LegacyBridgeParameterCollection Parameters => _parameters;

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        _connection.EnsureOpen();
        var dto = MapParameters();
        var resp = LegacyBridgeBlocking.Run(() => _connection.Session.ExecuteNonQueryAsync(CommandText, dto));
        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "executeNonQuery falhou");
        return resp.Result?.RowsAffected ?? 0;
    }

    public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        _connection.EnsureOpen();
        var dto = MapParameters();
        var resp = await _connection.Session.ExecuteNonQueryAsync(CommandText, dto, cancellationToken).ConfigureAwait(false);
        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "executeNonQuery falhou");
        return resp.Result?.RowsAffected ?? 0;
    }

    public override object? ExecuteScalar()
    {
        _connection.EnsureOpen();
        var dto = MapParameters();
        var resp = LegacyBridgeBlocking.Run(() => _connection.Session.ExecuteScalarAsync(CommandText, dto));
        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "executeScalar falhou");
        return NormalizeScalar(resp.Result?.Scalar);
    }

    public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        _connection.EnsureOpen();
        var dto = MapParameters();
        var resp = await _connection.Session.ExecuteScalarAsync(CommandText, dto, cancellationToken).ConfigureAwait(false);
        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "executeScalar falhou");
        return NormalizeScalar(resp.Result?.Scalar);
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new LegacyBridgeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        if (_activeReader != null)
            throw new InvalidOperationException("DataReader já aberto.");

        _connection.EnsureOpen();
        var dto = MapParameters();
        var resp = LegacyBridgeBlocking.Run(() => _connection.Session.ExecuteReaderAsync(CommandText, dto));
        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "executeReader falhou");

        var cols = resp.Result?.Columns ?? Array.Empty<string>();
        var rawRows = resp.Result?.Rows ?? new List<List<object?>>();
        var rows = NormalizeRows(rawRows);
        _activeReader = new LegacyBridgeDbDataReader(this, cols, rows);
        return _activeReader;
    }

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        if (_activeReader != null)
            throw new InvalidOperationException("DataReader já aberto.");

        _connection.EnsureOpen();
        var dto = MapParameters();
        var resp = await _connection.Session.ExecuteReaderAsync(CommandText, dto, cancellationToken).ConfigureAwait(false);
        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "executeReader falhou");

        var cols = resp.Result?.Columns ?? Array.Empty<string>();
        var rawRows = resp.Result?.Rows ?? new List<List<object?>>();
        var rows = NormalizeRows(rawRows);
        _activeReader = new LegacyBridgeDbDataReader(this, cols, rows);
        return _activeReader;
    }

    internal void NotifyReaderClosed() => _activeReader = null;

    private SqlParameterDto[]? MapParameters()
    {
        if (_parameters.Count == 0)
            return null;

        var list = new List<SqlParameterDto>(_parameters.Count);
        foreach (var p in _parameters.Items)
        {
            if (p.Value == null || p.Value == DBNull.Value)
                list.Add(new SqlParameterDto { Name = p.ParameterName, Type = "null" });
            else
                list.Add(MapParameterValue(p));
        }

        return list.ToArray();
    }

    private static SqlParameterDto MapParameterValue(LegacyBridgeDbParameter p)
    {
        var dto = new SqlParameterDto { Name = p.ParameterName };
        switch (p.DbType)
        {
            case DbType.Int32:
                dto.Type = "int";
                dto.Value = JsonSerializer.SerializeToElement(Convert.ToInt32(p.Value));
                break;
            case DbType.Int64:
                dto.Type = "long";
                dto.Value = JsonSerializer.SerializeToElement(Convert.ToInt64(p.Value));
                break;
            case DbType.Double:
            case DbType.Single:
                dto.Type = "double";
                dto.Value = JsonSerializer.SerializeToElement(Convert.ToDouble(p.Value));
                break;
            case DbType.Boolean:
                dto.Type = "bool";
                dto.Value = JsonSerializer.SerializeToElement(Convert.ToBoolean(p.Value));
                break;
            case DbType.Binary:
                dto.Type = "bytes";
                dto.Value = JsonSerializer.SerializeToElement(Convert.ToBase64String((byte[])p.Value!));
                break;
            default:
                dto.Type = "string";
                dto.Value = JsonSerializer.SerializeToElement(p.Value!.ToString());
                break;
        }

        return dto;
    }

    private static List<object?[]> NormalizeRows(List<List<object?>> rawRows)
    {
        var rows = new List<object?[]>();
        foreach (var r in rawRows)
        {
            var row = new object?[r.Count];
            for (var i = 0; i < r.Count; i++)
                row[i] = NormalizeCell(r[i]);
            rows.Add(row);
        }

        return rows;
    }

    private static object? NormalizeCell(object? v)
    {
        if (v is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => je.GetRawText()
            };
        }

        return v;
    }

    private static object? NormalizeScalar(object? v) => NormalizeCell(v);
}
