using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sqlite.LegacyBridge.Protocol;

/// <summary>Wire framing: one JSON object per line (NDJSON).</summary>
public sealed class BridgeEnvelope
{
    [JsonPropertyName("v")]
    public int ProtocolVersion { get; set; } = BridgeProtocolVersion.Current;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }
}
