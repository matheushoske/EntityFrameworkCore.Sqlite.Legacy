using System.Text.Json.Serialization;

namespace Sqlite.LegacyBridge.Protocol;

public sealed class BridgeResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("result")]
    public BridgeResult? Result { get; set; }
}

public sealed class BridgeResult
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("columns")]
    public string[]? Columns { get; set; }

    /// <summary>Each row: JSON primitives (string, number, bool), or null. Blob as base64 string.</summary>
    [JsonPropertyName("rows")]
    public List<List<object?>>? Rows { get; set; }

    [JsonPropertyName("rowsAffected")]
    public int? RowsAffected { get; set; }

    [JsonPropertyName("scalar")]
    public object? Scalar { get; set; }

    [JsonPropertyName("transactionId")]
    public int? TransactionId { get; set; }
}
