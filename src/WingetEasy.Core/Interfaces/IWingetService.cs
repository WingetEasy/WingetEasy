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
    Task<IEnumerable<WingetPackage>> GetInstalledPackagesAsync(CancellationToken ct = default);
    Task<bool> IsWingetAvailableAsync();
    Task<string> GetWingetVersionAsync();
}

