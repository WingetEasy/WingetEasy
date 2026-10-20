using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Orquestra a criação, restauração e exportação de snapshots do sistema.
/// </summary>
public interface IBackupService
{
    Task<BackupSnapshot> CreateSnapshotAsync(string label, CancellationToken ct = default);
    Task<IReadOnlyList<UpdateResult>> RestoreSnapshotAsync(
        Guid snapshotId,
        IProgress<(string packageId, int current, int total)>? progress = null,
        int maxRetries = 1,
        CancellationToken ct = default);
    Task ExportScriptAsync(Guid snapshotId, string path, CancellationToken ct = default);
}
