using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Define o contrato principal para a interação direta com o Windows Package Manager (Winget CLI).
/// Responsável pelas operações de baixo nível como procurar, listar e instalar pacotes individuais.
/// </summary>

public interface IWingetService
{
    Task<IEnumerable<WingetPackage>> CheckUpdatesAsync(CancellationToken ct = default);
    Task<UpdateResult> InstallUpdateAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Instala um pacote (winget install). Por padrão recai sobre InstallUpdateAsync para implementações que não o sobrescrevam.
    /// </summary>
    Task<UpdateResult> InstallPackageAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default) =>
        InstallUpdateAsync(packageId, progress, ct);
    Task<IEnumerable<WingetPackage>> GetInstalledPackagesAsync(CancellationToken ct = default);
    Task<bool> IsWingetAvailableAsync();
    Task<string> GetWingetVersionAsync();
    Task<bool> RepairSourcesAsync(CancellationToken ct = default);
    Task<bool> PinPackageAsync(string packageId, CancellationToken ct = default);
    Task<bool> UnpinPackageAsync(string packageId, CancellationToken ct = default);
}
