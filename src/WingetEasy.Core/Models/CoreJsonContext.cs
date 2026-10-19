using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WingetEasy.Core.Models;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ElevatedJob))]
[JsonSerializable(typeof(List<UpdateResult>))]
public partial class CoreJsonContext : JsonSerializerContext
{
}
