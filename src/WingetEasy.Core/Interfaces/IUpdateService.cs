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
    Task<IReadOnlyList<UpdateResult>> ExecuteUpdatesAsync(IEnumerable<string> packageIds, IProgress<(string name, int pct, int cur, int total)>? progress = null, CancellationToken ct = default);
}
