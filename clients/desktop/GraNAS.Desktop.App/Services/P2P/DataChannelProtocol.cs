using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraNAS.Desktop.App.Services.P2P;

// ── Сообщения, приходящие от receiver (входящие для owner) ──────────────────

/// <summary>Инициирует ECDH-рукопожатие: receiver отправляет свой публичный ключ (SPKI, Base64).</summary>
public record EcdhOfferMessage([property: JsonPropertyName("type")] string Type,
                                [property: JsonPropertyName("publicKey")] string PublicKey);

/// <summary>Запрос на получение списка файлов в папке (с учётом scope path, если задан).</summary>
public record ListRequestMessage([property: JsonPropertyName("type")] string Type);

/// <summary>Запрос на передачу конкретного файла по относительному пути.</summary>
/// <param name="Path">Относительный путь к файлу внутри shared-папки.</param>
public record FileRequestMessage([property: JsonPropertyName("type")] string Type,
                                  [property: JsonPropertyName("path")] string Path);

// ── Сообщения, отправляемые receiver'у (исходящие от owner) ─────────────────

/// <summary>Ответ на ECDH-рукопожатие: owner отправляет свой публичный ключ (SPKI, Base64).</summary>
public record EcdhAnswerMessage([property: JsonPropertyName("type")] string Type,
                                 [property: JsonPropertyName("publicKey")] string PublicKey);

/// <summary>Запись о файле в списке содержимого папки.</summary>
public record RemoteFileEntry([property: JsonPropertyName("path")] string Path,
                               [property: JsonPropertyName("size")] long Size,
                               [property: JsonPropertyName("modifiedAt")] DateTime ModifiedAt);

/// <summary>Ответ на <see cref="ListRequestMessage"/>: список файлов в папке (рекурсивно).</summary>
public record ListResponseMessage([property: JsonPropertyName("type")] string Type,
                                   [property: JsonPropertyName("files")] RemoteFileEntry[] Files);

/// <summary>
/// Метаданные файла перед отправкой бинарных чанков.
/// После этого сообщения owner начинает передавать зашифрованные чанки в бинарном формате.
/// </summary>
/// <param name="Sha256">SHA-256 хеш plaintext-содержимого (hex, lowercase) для верификации на receiver'е.</param>
/// <param name="Iv">Зарезервировано (не используется, nonce встроен в каждый чанк).</param>
public record FileHeaderMessage([property: JsonPropertyName("type")] string Type,
                                 [property: JsonPropertyName("path")] string Path,
                                 [property: JsonPropertyName("size")] long Size,
                                 [property: JsonPropertyName("sha256")] string Sha256,
                                 [property: JsonPropertyName("iv")] string Iv);

/// <summary>Сигнализирует об успешном завершении передачи файла.</summary>
public record FileCompleteMessage([property: JsonPropertyName("type")] string Type,
                                   [property: JsonPropertyName("path")] string Path);

/// <summary>Сообщение об ошибке при обработке запроса.</summary>
/// <param name="Code">Код ошибки: <c>NOT_FOUND</c>, <c>FORBIDDEN</c>, <c>READ_ERROR</c>.</param>
public record DataChannelErrorMessage([property: JsonPropertyName("type")] string Type,
                                       [property: JsonPropertyName("code")] string Code,
                                       [property: JsonPropertyName("message")] string Message);

/// <summary>
/// Константы типов JSON-сообщений протокола data channel.
/// Значение поля <c>type</c> в каждом фрейме.
/// </summary>
public static class ProtocolMessageType
{
    /// <summary>Receiver отправляет свой ECDH публичный ключ.</summary>
    public const string EcdhOffer = "ecdh_offer";
    /// <summary>Owner отвечает своим ECDH публичным ключом.</summary>
    public const string EcdhAnswer = "ecdh_answer";
    /// <summary>Receiver запрашивает список файлов.</summary>
    public const string ListRequest = "list_request";
    /// <summary>Owner возвращает список файлов.</summary>
    public const string ListResponse = "list_response";
    /// <summary>Receiver запрашивает конкретный файл.</summary>
    public const string FileRequest = "file_request";
    /// <summary>Owner отправляет метаданные файла перед бинарными чанками.</summary>
    public const string FileHeader = "file_header";
    /// <summary>Owner подтверждает завершение передачи файла.</summary>
    public const string FileComplete = "file_complete";
    /// <summary>Сообщение об ошибке.</summary>
    public const string Error = "error";
}

/// <summary>
/// Утилиты сериализации/десериализации JSON-фреймов протокола data channel.
/// Контракт совпадает с web-receiver (TypeScript).
/// </summary>
public static class ProtocolSerializer
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    /// <summary>Сериализует сообщение протокола в JSON-строку.</summary>
    public static string Serialize<T>(T message) => JsonSerializer.Serialize(message, Opts);

    /// <summary>Извлекает значение поля <c>type</c> из JSON-фрейма без полной десериализации. Возвращает <c>null</c> при ошибке.</summary>
    public static string? GetMessageType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>Десериализует JSON-фрейм в конкретный тип сообщения. Возвращает <c>default</c> при ошибке парсинга.</summary>
    public static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, Opts); }
        catch { return default; }
    }
}
