using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sqlite.LegacyBridge.Protocol.Payloads;

public sealed class SqlParameterDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Discriminator: int, long, double, string, bytes (base64), null</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "null";

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }
}
