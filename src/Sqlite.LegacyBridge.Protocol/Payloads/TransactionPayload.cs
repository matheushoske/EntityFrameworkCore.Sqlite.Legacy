using System.Text.Json.Serialization;

namespace Sqlite.LegacyBridge.Protocol.Payloads;

public sealed class TransactionPayload
{
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set; }
}
