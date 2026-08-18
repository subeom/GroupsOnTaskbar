using System.Text.Json.Serialization;
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Configuration;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(LauncherConfiguration))]
internal sealed partial class LauncherConfigurationJsonContext : JsonSerializerContext
{
}
