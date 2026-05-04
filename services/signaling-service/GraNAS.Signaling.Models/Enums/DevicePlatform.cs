using System.Text.Json.Serialization;

namespace GraNAS.Signaling.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DevicePlatform
{
    Windows,
    Linux,
    MacOS,
    Web
}
