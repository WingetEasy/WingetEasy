using System;

namespace WingetEasy.Core.Models;

/// <summary>
/// Representa uma regra persistida de supressão, adiamento ou fixação de um pacote.
/// </summary>
public record PackageSuppressionRule(
    string PackageId,
    string Name,
    string Source,
    bool IsPermanentlySkipped,
    DateTime? DeferredUntilUtc = null,
    string? IgnoredVersion = null,
    string? Reason = null,
    DateTime? UpdatedAtUtc = null
);

