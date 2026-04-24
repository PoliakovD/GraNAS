using System.Text.Json.Serialization;

namespace GraNAS.Metadata.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessLevel
{
  View,
  Full
}
