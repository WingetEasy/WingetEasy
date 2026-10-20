using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public sealed class BackupSnapshotRepository : IBackupSnapshotRepository
{
    private readonly AppDbContext _dbContext;

    public BackupSnapshotRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task SaveAsync(BackupSnapshot snapshot, CancellationToken ct = default)
    {
        var entity = new BackupSnapshotEntity
        {
            Id = snapshot.Id,
            Label = snapshot.Label,
            CreatedAt = snapshot.CreatedAt,
            PackageCount = snapshot.PackageCount,
            WingetVersion = snapshot.WingetVersion,
            Data = JsonSerializer.Serialize(snapshot.Packages ?? [])
        };
        _dbContext.BackupSnapshots.Add(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<BackupSnapshot?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.BackupSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.Id == id, ct).ConfigureAwait(false);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<BackupSnapshot>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _dbContext.BackupSnapshots.AsNoTracking()
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        return entities.Select(ToModel).ToList();
    }

    private static BackupSnapshot ToModel(BackupSnapshotEntity entity)
    {
        var packages = JsonSerializer.Deserialize<List<BackupPackage>>(entity.Data) ?? [];
        return new BackupSnapshot(entity.Id, entity.Label, entity.CreatedAt, entity.PackageCount, entity.WingetVersion, packages);
    }
}
