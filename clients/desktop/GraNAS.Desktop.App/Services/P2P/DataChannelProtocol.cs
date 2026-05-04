using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraNAS.Desktop.App.Services.P2P;

// Incoming messages (from receiver via data channel)
public record EcdhOfferMessage([property: JsonPropertyName("type")] string Type,
                                [property: JsonPropertyName("publicKey")] string PublicKey);

public record ListRequestMessage([property: JsonPropertyName("type")] string Type);

public record FileRequestMessage([property: JsonPropertyName("type")] string Type,
                                  [property: JsonPropertyName("path")] string Path);

// Outgoing messages (to receiver via data channel)
public record EcdhAnswerMessage([property: JsonPropertyName("type")] string Type,
                                 [property: JsonPropertyName("publicKey")] string PublicKey);

public record RemoteFileEntry([property: JsonPropertyName("path")] string Path,
                               [property: JsonPropertyName("size")] long Size,
                               [property: JsonPropertyName("modifiedAt")] DateTime ModifiedAt);

public record ListResponseMessage([property: JsonPropertyName("type")] string Type,
                                   [property: JsonPropertyName("files")] RemoteFileEntry[] Files);

public record FileHeaderMessage([property: JsonPropertyName("type")] string Type,
                                 [property: JsonPropertyName("path")] string Path,
                                 [property: JsonPropertyName("size")] long Size,
                                 [property: JsonPropertyName("sha256")] string Sha256,
                                 [property: JsonPropertyName("iv")] string Iv);

public record FileCompleteMessage([property: JsonPropertyName("type")] string Type,
                                   [property: JsonPropertyName("path")] string Path);

public record DataChannelErrorMessage([property: JsonPropertyName("type")] string Type,
                                       [property: JsonPropertyName("code")] string Code,
                                       [property: JsonPropertyName("message")] string Message);

public static class ProtocolMessageType
{
    public const string EcdhOffer = "ecdh_offer";
    public const string EcdhAnswer = "ecdh_answer";
    public const string ListRequest = "list_request";
    public const string ListResponse = "list_response";
    public const string FileRequest = "file_request";
    public const string FileHeader = "file_header";
    public const string FileComplete = "file_complete";
    public const string Error = "error";
}

public static class ProtocolSerializer
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T message) => JsonSerializer.Serialize(message, Opts);

    public static string? GetMessageType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        }
        catch { return null; }
    }

    public static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, Opts); }
        catch { return default; }
    }
}
