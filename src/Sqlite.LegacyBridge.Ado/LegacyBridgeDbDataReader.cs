using System.Collections;
using System.Data.Common;

namespace Sqlite.LegacyBridge.Ado;

public sealed class LegacyBridgeDbDataReader : DbDataReader
{
    private readonly LegacyBridgeDbCommand _command;
    private readonly string[] _columns;
    private readonly List<object?[]> _rows;
    private int _row = -1;
    private bool _closed;

    public LegacyBridgeDbDataReader(LegacyBridgeDbCommand command, string[] columns, List<object?[]> rows)
    {
        _command = command;
        _columns = columns;
        _rows = rows;
    }

    public override int FieldCount => _columns.Length;
    public override int RecordsAffected => -1;
    public override bool HasRows => _rows.Count > 0;
    public override bool IsClosed => _closed;
    public override int Depth => 0;

    public override string GetName(int ordinal) => _columns[ordinal];

    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < _columns.Length; i++)
        {
            if (string.Equals(_columns[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new IndexOutOfRangeException(name);
    }

    public override object GetValue(int ordinal) => _rows[_row][ordinal] ?? DBNull.Value;

    public override bool IsDBNull(int ordinal) => _rows[_row][ordinal] == null;

    public override bool Read()
    {
        _row++;
        return _row < _rows.Count;
    }

    public override bool NextResult() => false;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int GetValues(object[] values)
    {
        var n = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < n; i++)
            values[i] = GetValue(i);
        return n;
    }

    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    public override Type GetFieldType(int ordinal)
    {
        if (_row >= 0 && _row < _rows.Count)
        {
            var v = _rows[_row][ordinal];
            if (v != null)
                return v.GetType();
        }

        if (_rows.Count > 0)
        {
            var v0 = _rows[0][ordinal];
            if (v0 != null)
                return v0.GetType();
        }

        return typeof(string);
    }

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override Guid GetGuid(int ordinal) => Guid.Parse(GetValue(ordinal).ToString()!);
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override string GetString(int ordinal) => GetValue(ordinal).ToString()!;

    public override void Close()
    {
        _closed = true;
        _command.NotifyReaderClosed();
    }
}
