using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Persiste e recupera snapshots de pacotes instalados.
/// </summary>
public interface IBackupSnapshotRepository
{
    Task SaveAsync(BackupSnapshot snapshot, CancellationToken ct = default);
    Task<BackupSnapshot?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BackupSnapshot>> GetAllAsync(CancellationToken ct = default);
}
