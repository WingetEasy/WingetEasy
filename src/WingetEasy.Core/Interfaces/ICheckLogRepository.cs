using System;
using System.Threading;
using System.Threading.Tasks;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Contrato para o armazenamento do histórico de verificações de atualizações.
/// </summary>

public interface ICheckLogRepository
{
    Task AddAsync(int foundCount, long durationMs, CancellationToken ct = default);
    Task<DateTime?> GetLastCheckDateAsync(CancellationToken ct = default);
}


