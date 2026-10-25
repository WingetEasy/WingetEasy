using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class UpdateHistoryRepository : IUpdateHistoryRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    [ActivatorUtilitiesConstructor]
    public UpdateHistoryRepository(IDbContextFactory<AppDbContext> contextFactory) => _contextFactory = contextFactory;

    public UpdateHistoryRepository(AppDbContext dbContext)
    {
        _contextFactory = new AppDbContext.DelegatingFactory(dbContext.Database.GetConnectionString());
    }

    public async Task AddAsync(UpdateResult result, CancellationToken ct = default)
    {
        var entity = new UpdateHistoryEntity
        {
            PackageId = result.PackageId,
            PackageName = result.PackageName,
            FromVersion = result.FromVersion ?? string.Empty,
            ToVersion = result.ToVersion ?? string.Empty,
            Status = result.Success ? UpdateStatus.Success : UpdateStatus.Failed,
            ErrorMessage = result.ErrorMessage,
            DurationMs = result.Duration.HasValue ? (long)result.Duration.Value.TotalMilliseconds : 0,
            UpdatedAt = result.UpdatedAt ?? DateTime.UtcNow,
        };

        using var _dbContext = _contextFactory.CreateDbContext();
        _dbContext.UpdateHistories.Add(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IEnumerable<UpdateResult>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var entities = await _dbContext.UpdateHistories.AsNoTracking()
            .OrderByDescending(u => u.UpdatedAt)
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(ToResult);
    }

    public async Task<IEnumerable<UpdateResult>> GetByDataRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var entities = await _dbContext.UpdateHistories.AsNoTracking()
            .Where(u => u.UpdatedAt >= start && u.UpdatedAt <= end)
            .OrderByDescending(u => u.UpdatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(ToResult);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        return await _dbContext.UpdateHistories.CountAsync(ct).ConfigureAwait(false);
    }

    private static UpdateResult ToResult(UpdateHistoryEntity entity) =>
        new(
            entity.PackageId,
            entity.PackageName,
            entity.Status == UpdateStatus.Success,
            entity.ErrorMessage,
            TimeSpan.FromMilliseconds(entity.DurationMs),
            entity.FromVersion,
            entity.ToVersion,
            entity.UpdatedAt);
}
