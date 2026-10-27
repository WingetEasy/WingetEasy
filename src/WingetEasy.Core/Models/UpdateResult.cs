using System;

namespace WingetEasy.Core.Models;

public record UpdateResult(
    string PackageId,
    string PackageName,
    bool Success,
    string? ErrorMessage = null,
    TimeSpan? Duration = null,
    string FromVersion = "",
    string ToVersion = "",
    DateTime? UpdatedAt = null,
    int? ExitCode = null,
    bool IsApplicationRunning = false,
    bool Skipped = false,
    bool RequiresReboot = false,
    bool RequiresElevation = false,
    bool CanRetryInteractive = false
);
