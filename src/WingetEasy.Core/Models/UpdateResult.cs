using System;

namespace WingetEasy.Core.Models;

public record UpdateResult(
    string PackageId,
    string PackageName,
    bool Success,
    string? ErrorMessage = null,
    TimeSpan? Duration = null
);
