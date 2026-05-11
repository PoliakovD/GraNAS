using System.Text.Json.Serialization;

namespace GraNAS.Signaling.Models.Enums;

/// <summary>Платформа клиентского устройства. Сериализуется в JSON как строка.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DevicePlatform
{
    Windows,
    Linux,
    MacOS,
    /// <summary>Браузерный web-клиент. В текущей реализации выступает только в роли receiver.</summary>
    Web
}
