using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Contrato para o armazenamento do histórico de atualizações realizadas com sucesso ou falha.
/// </summary>

public interface IUpdateHistoryRepository
{
    Task AddAsync(UpdateResult result, CancellationToken ct = default);
    Task<IEnumerable<UpdateResult>> GetRecentAsync(int count, CancellationToken ct = default);
    Task<IEnumerable<UpdateResult>> GetByDataRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
