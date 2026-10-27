using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Orquestra o processo de atualização de alto nível do sistema.
/// Responsável por processar filas de pacotes, calcular o progresso global e lidar com falhas em lote.
/// </summary>
public interface IUpdateService
{
    Task<IReadOnlyList<UpdateResult>> ExecuteUpdatesAsync(
        IEnumerable<string> packageIds,
        IProgress<(string name, int pct, int cur, int total)>? progress = null,
        Func<Task<bool>>? confirmElevation = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<UpdateResult>> ExecuteUpdatesAsync(
        IEnumerable<string> packageIds,
        IProgress<(string name, int pct, int cur, int total)>? progress,
        Func<Task<bool>>? confirmElevation,
        Func<string, Task<AppRunningResolution>>? handleRunningApp,
        bool isAutoUpdate = false,
        CancellationToken ct = default,
        IProgress<(string packageId, string phase)>? phaseProgress = null,
        IReadOnlyDictionary<string, PackageUpdateMetadata>? packageMetadata = null);
}
