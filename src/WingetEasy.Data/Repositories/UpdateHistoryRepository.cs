using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class UpdateHistoryRepository : IUpdateHistoryRepository
{
    private readonly AppDbContext _dbContext;

    public UpdateHistoryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(UpdateResult result, CancellationToken ct = default)
    {
        var entity = new UpdateHistoryEntity
        {
            PackageId = result.PackageId,
            PackageName = result.PackageName,
            FromVersion = string.Empty, // Compatibilidade com a entidade atual
            ToVersion = string.Empty, // Compatibilidade com a entidade atual
            Status = result.Success ? UpdateStatus.Success : UpdateStatus.Failed,
            ErrorMessage = result.ErrorMessage,
            DurationMs = result.Duration.HasValue ? (long)result.Duration.Value.TotalMilliseconds : 0,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.UpdateHistories.Add(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IEnumerable<UpdateResult>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        var entities = await _dbContext.UpdateHistories
            .OrderByDescending(u => u.UpdatedAt)
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(e => new UpdateResult(e.PackageId, e.PackageName, e.Status == UpdateStatus.Success, e.ErrorMessage, TimeSpan.FromMilliseconds(e.DurationMs)));
    }

    public async Task<IEnumerable<UpdateResult>> GetByDataRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var entities = await _dbContext.UpdateHistories
            .Where(u => u.UpdatedAt >= start && u.UpdatedAt <= end)
            .OrderByDescending(u => u.UpdatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(e => new UpdateResult(e.PackageId, e.PackageName, e.Status == UpdateStatus.Success, e.ErrorMessage, TimeSpan.FromMilliseconds(e.DurationMs)));
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await _dbContext.UpdateHistories.CountAsync(ct).ConfigureAwait(false);
    }
}

