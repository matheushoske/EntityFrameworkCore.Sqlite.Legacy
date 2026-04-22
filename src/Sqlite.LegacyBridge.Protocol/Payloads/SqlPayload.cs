using System.Text.Json.Serialization;

namespace Sqlite.LegacyBridge.Protocol.Payloads;

public sealed class SqlPayload
{
    [JsonPropertyName("sql")]
    public string Sql { get; set; } = "";

    [JsonPropertyName("parameters")]
    public SqlParameterDto[]? Parameters { get; set; }
}
