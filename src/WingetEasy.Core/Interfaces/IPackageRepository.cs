using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Contrato para gerir regras locais de pacotes, como a lista de aplicações ignoradas (skipped) pelo utilizador.
/// </summary>

public interface IPackageRepository
{
    Task<IEnumerable<string>> GetSkippedIdsAsync(CancellationToken ct = default);
    Task SkipPackageAsync(string packageId, string? reason = null, CancellationToken ct = default);
    Task UnskipPackageAsync(string packageId, CancellationToken ct = default);
}
