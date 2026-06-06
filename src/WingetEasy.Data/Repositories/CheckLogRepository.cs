using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WingetEasy.Core.Interfaces;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class CheckLogRepository : ICheckLogRepository
{
    private readonly AppDbContext _dbContext;

    public CheckLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(int foundCount, long durationMs, CancellationToken ct = default)
    {
        var entity = new CheckLogEntity
        {
            FoundCount = foundCount,
            DurationMs = durationMs,
            CheckedAt = DateTime.UtcNow
        };

        _dbContext.CheckLogs.Add(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<DateTime?> GetLastCheckDateAsync(CancellationToken ct = default)
    {
        var log = await _dbContext.CheckLogs
            .OrderByDescending(c => c.CheckedAt)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return log?.CheckedAt;
    }

}
