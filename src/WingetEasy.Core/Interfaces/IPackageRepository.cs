using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Contrato para gerir regras locais de pacotes, como a lista de aplicações ignoradas (skipped),
/// adiadas temporariamente ou com versão ignorada pelo utilizador.
/// </summary>

public interface IPackageRepository
{
    Task<IEnumerable<string>> GetSkippedIdsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PackageSuppressionRule>> GetSuppressionRulesAsync(CancellationToken ct = default);
    Task SkipPackageAsync(string packageId, string? reason = null, CancellationToken ct = default);
    Task DeferPackageUntilAsync(string packageId, DateTime deferredUntilUtc, string? reason = null, CancellationToken ct = default);
    Task IgnorePackageVersionAsync(string packageId, string version, string? reason = null, CancellationToken ct = default);
    Task UnskipPackageAsync(string packageId, CancellationToken ct = default);
}
