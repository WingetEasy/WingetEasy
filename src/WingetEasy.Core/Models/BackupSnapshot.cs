using System;

namespace WingetEasy.Core.Models;

public record BackupSnapshot(
    Guid Id,
    string Label,
    DateTime CreatedAt,
    int PackageCount,
    string WingetVersion
);
