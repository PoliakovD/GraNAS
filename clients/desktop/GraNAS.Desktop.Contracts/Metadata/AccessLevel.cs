using System.Text.Json.Serialization;

namespace GraNAS.Desktop.Contracts.Metadata;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessLevel
{
  View,
  Full
}
