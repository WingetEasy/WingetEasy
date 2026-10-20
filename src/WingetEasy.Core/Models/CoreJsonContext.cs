using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WingetEasy.Core.Models;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ElevatedJob))]
[JsonSerializable(typeof(List<UpdateResult>))]
[JsonSerializable(typeof(BackupSnapshot))]
[JsonSerializable(typeof(List<BackupPackage>))]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(ProfilePackage))]
[JsonSerializable(typeof(List<Profile>))]
[JsonSerializable(typeof(List<ProfilePackage>))]
public partial class CoreJsonContext : JsonSerializerContext
{
}
