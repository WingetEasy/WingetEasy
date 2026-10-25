using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WingetEasy.Core.Interfaces;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class CheckLogRepository : ICheckLogRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    [ActivatorUtilitiesConstructor]
    public CheckLogRepository(IDbContextFactory<AppDbContext> contextFactory) => _contextFactory = contextFactory;

    public CheckLogRepository(AppDbContext dbContext)
    {
        _contextFactory = new AppDbContext.DelegatingFactory(dbContext.Database.GetConnectionString());
    }

    public async Task AddAsync(int foundCount, long durationMs, CancellationToken ct = default)
    {
        var entity = new CheckLogEntity
        {
            FoundCount = foundCount,
            DurationMs = durationMs,
            CheckedAt = DateTime.UtcNow
        };

        using var _dbContext = _contextFactory.CreateDbContext();
        _dbContext.CheckLogs.Add(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<DateTime?> GetLastCheckDateAsync(CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var log = await _dbContext.CheckLogs.AsNoTracking()
            .OrderByDescending(c => c.CheckedAt)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return log?.CheckedAt;
    }

}
