using System.Globalization;
using System.Text.Json;
using Sqlite.LegacyBridge.Protocol.Payloads;

namespace Sqlite.LegacyBridge.Protocol;

/// <summary>Shared helpers for host (System.Data.SQLite) and tests.</summary>
public static class ParameterBinder
{
    public static void AddParameters(System.Data.IDbCommand command, SqlParameterDto[]? parameters)
    {
        if (parameters == null || parameters.Length == 0)
            return;

        foreach (var p in parameters)
        {
            var param = command.CreateParameter();
            param.ParameterName = p.Name.StartsWith("@", StringComparison.Ordinal) ? p.Name : "@" + p.Name;
            SetValue(param, p);
            command.Parameters.Add(param);
        }
    }

    private static void SetValue(System.Data.IDataParameter param, SqlParameterDto dto)
    {
        var t = dto.Type?.ToLowerInvariant() ?? "null";
        if (t == "null" || dto.Value is not { } jsonEl || jsonEl.ValueKind == JsonValueKind.Null)
        {
            param.Value = DBNull.Value;
            return;
        }

        var el = jsonEl;
        switch (t)
        {
            case "int":
            case "int32":
                param.Value = el.GetInt32();
                return;
            case "long":
            case "int64":
                param.Value = el.GetInt64();
                return;
            case "double":
                param.Value = el.GetDouble();
                return;
            case "bool":
            case "boolean":
                param.Value = el.GetBoolean();
                return;
            case "string":
                param.Value = el.GetString() ?? (object)DBNull.Value;
                return;
            case "bytes":
            case "blob":
                var s = el.GetString();
                param.Value = string.IsNullOrEmpty(s) ? (object)DBNull.Value : Convert.FromBase64String(s);
                return;
            case "decimal":
                param.Value = decimal.Parse(el.GetString() ?? "0", CultureInfo.InvariantCulture);
                return;
            default:
                if (el.ValueKind == JsonValueKind.String)
                    param.Value = el.GetString() ?? (object)DBNull.Value;
                else if (el.ValueKind == JsonValueKind.Number)
                {
                    if (el.TryGetInt64(out var l))
                        param.Value = l;
                    else
                        param.Value = el.GetDouble();
                }
                else if (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
                    param.Value = el.GetBoolean();
                else
                    param.Value = el.GetRawText();
                return;
        }
    }
}
