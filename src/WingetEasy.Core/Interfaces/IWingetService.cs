using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Contrato para interação de baixo nível com a CLI do Windows Package Manager (winget.exe).
/// </summary>
public interface IWingetService
{
    Task<IEnumerable<WingetPackage>> CheckUpdatesAsync(CancellationToken ct = default);
    Task<UpdateResult> InstallUpdateAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<UpdateResult> InstallUpdateAsync(string packageId, IProgress<int>? progress, CancellationToken ct, IProgress<string>? phaseProgress);

    /// <summary>
    /// Instala um pacote (winget install). Por padrão recai sobre InstallUpdateAsync para implementações que não o sobrescrevam.
    /// </summary>
    Task<UpdateResult> InstallPackageAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default) =>
        InstallUpdateAsync(packageId, progress, ct);

    Task<UpdateResult> InstallPackageAsync(string packageId, IProgress<int>? progress, CancellationToken ct, IProgress<string>? phaseProgress) =>
        InstallUpdateAsync(packageId, progress, ct, phaseProgress);

    Task<IEnumerable<WingetPackage>> GetInstalledPackagesAsync(CancellationToken ct = default);
    Task<bool> IsWingetAvailableAsync();
    Task<string> GetWingetVersionAsync();
    Task<bool> RepairSourcesAsync(CancellationToken ct = default);
    Task<bool> PinPackageAsync(string packageId, CancellationToken ct = default);
    Task<bool> UnpinPackageAsync(string packageId, CancellationToken ct = default);
}
