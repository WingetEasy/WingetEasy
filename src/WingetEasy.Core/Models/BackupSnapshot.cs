using System;

namespace WingetEasy.Core.Models;

public record BackupSnapshot(
    Guid Id,
    string Label,
    DateTime CreatedAt,
    int PackageCount,
    string WingetVersion,
    IReadOnlyList<BackupPackage>? Packages = null
);

public record BackupPackage(string Id, string Name, string Version);
