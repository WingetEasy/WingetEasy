using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public sealed class BackupSnapshotRepository : IBackupSnapshotRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    [ActivatorUtilitiesConstructor]
    public BackupSnapshotRepository(IDbContextFactory<AppDbContext> contextFactory) => _contextFactory = contextFactory;

    public BackupSnapshotRepository(AppDbContext dbContext) => _contextFactory = new AppDbContext.DelegatingFactory(dbContext.Database.GetConnectionString());

    public async Task SaveAsync(BackupSnapshot snapshot, CancellationToken ct = default)
    {
        var entity = new BackupSnapshotEntity
        {
            Id = snapshot.Id,
            Label = snapshot.Label,
            CreatedAt = snapshot.CreatedAt,
            PackageCount = snapshot.PackageCount,
            WingetVersion = snapshot.WingetVersion,
            Data = JsonSerializer.Serialize(snapshot.Packages?.ToList() ?? [], CoreJsonContext.Default.ListBackupPackage)
        };
        using var _dbContext = _contextFactory.CreateDbContext();
        _dbContext.BackupSnapshots.Add(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<BackupSnapshot?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var entity = await _dbContext.BackupSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.Id == id, ct).ConfigureAwait(false);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<BackupSnapshot>> GetAllAsync(CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var entities = await _dbContext.BackupSnapshots.AsNoTracking()
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        return entities.Select(ToModel).ToList();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var snapshotId))
        {
            throw new ArgumentException("O identificador do snapshot é inválido.", nameof(id));
        }

        using var _dbContext = _contextFactory.CreateDbContext();
        var entity = await _dbContext.BackupSnapshots
            .SingleOrDefaultAsync(snapshot => snapshot.Id == snapshotId, ct)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        _dbContext.BackupSnapshots.Remove(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static BackupSnapshot ToModel(BackupSnapshotEntity entity)
    {
        var packages = JsonSerializer.Deserialize(entity.Data, CoreJsonContext.Default.ListBackupPackage) ?? [];
        return new BackupSnapshot(entity.Id, entity.Label, entity.CreatedAt, entity.PackageCount, entity.WingetVersion, packages);
    }
}
