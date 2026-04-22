using System.Collections;
using System.Data;
using System.Data.Common;

namespace Sqlite.LegacyBridge.Ado;

public sealed class LegacyBridgeParameterCollection : DbParameterCollection
{
    private readonly List<LegacyBridgeDbParameter> _items = new();

    public override int Count => _items.Count;
    public override object SyncRoot => ((ICollection)_items).SyncRoot;
    public override int Add(object value)
    {
        if (value is LegacyBridgeDbParameter p)
        {
            _items.Add(p);
            return _items.Count - 1;
        }

        throw new ArgumentException("Esperado LegacyBridgeDbParameter.", nameof(value));
    }

    public override void AddRange(Array values)
    {
        foreach (var v in values)
            Add(v);
    }

    public override void Clear() => _items.Clear();

    public override bool Contains(object value) => value is LegacyBridgeDbParameter p && _items.Contains(p);

    public override bool Contains(string value) => _items.Any(x => x.ParameterName == value);

    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _items.GetEnumerator();

    public override int IndexOf(object value) => value is LegacyBridgeDbParameter p ? _items.IndexOf(p) : -1;

    public override int IndexOf(string parameterName)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i].ParameterName, parameterName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    public override void Insert(int index, object value)
    {
        if (value is LegacyBridgeDbParameter p)
            _items.Insert(index, p);
        else
            throw new ArgumentException(nameof(value));
    }

    public override bool IsFixedSize => false;
    public override bool IsReadOnly => false;
    public override bool IsSynchronized => false;

    public override void Remove(object value)
    {
        if (value is LegacyBridgeDbParameter p)
            _items.Remove(p);
    }

    public override void RemoveAt(string parameterName)
    {
        var i = IndexOf(parameterName);
        if (i >= 0)
            _items.RemoveAt(i);
    }

    public override void RemoveAt(int index) => _items.RemoveAt(index);

    protected override DbParameter GetParameter(int index) => _items[index];

    protected override DbParameter GetParameter(string parameterName)
    {
        var i = IndexOf(parameterName);
        if (i < 0)
            throw new IndexOutOfRangeException(parameterName);
        return _items[i];
    }

    protected override void SetParameter(int index, DbParameter value)
    {
        if (value is LegacyBridgeDbParameter p)
            _items[index] = p;
        else
            throw new ArgumentException(nameof(value));
    }

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var i = IndexOf(parameterName);
        if (i < 0)
            Add(value);
        else
            SetParameter(i, value);
    }

    internal IReadOnlyList<LegacyBridgeDbParameter> Items => _items;
}
