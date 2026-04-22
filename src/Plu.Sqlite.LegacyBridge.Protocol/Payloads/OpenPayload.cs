using System.Text.Json.Serialization;

namespace Sqlite.LegacyBridge.Protocol.Payloads;

public sealed class OpenPayload
{
    [JsonPropertyName("databasePath")]
    public string DatabasePath { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}
