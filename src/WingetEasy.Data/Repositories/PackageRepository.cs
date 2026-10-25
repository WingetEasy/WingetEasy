using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WingetEasy.Core.Interfaces;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class PackageRepository : IPackageRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    [ActivatorUtilitiesConstructor]
    public PackageRepository(IDbContextFactory<AppDbContext> contextFactory) => _contextFactory = contextFactory;

    public PackageRepository(AppDbContext dbContext)
    {
        _contextFactory = new AppDbContext.DelegatingFactory(dbContext.Database.GetConnectionString());
    }

    public async Task<IEnumerable<string>> GetSkippedIdsAsync(CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        return await _dbContext.Packages.AsNoTracking()
            .Where(p => p.IsSkipped)
            .Select(p => p.WingetId)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task SkipPackageAsync(string packageId, string? reason = null, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var package = await _dbContext.Packages.FirstOrDefaultAsync(p => p.WingetId == packageId, ct).ConfigureAwait(false);

        if (package == null)
        {
            package = new PackageEntity
            {
                WingetId = packageId,
                Name = packageId, // Fallback se não existir
                Source = "winget",
                IsSkipped = true,
                SkipReason = reason,
                SkippedAt = DateTime.UtcNow
            };
            _dbContext.Packages.Add(package);
        }
        else
        {
            package.IsSkipped = true;
            package.SkipReason = reason;
            package.SkippedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UnskipPackageAsync(string packageId, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var package = await _dbContext.Packages.FirstOrDefaultAsync(p => p.WingetId == packageId, ct).ConfigureAwait(false);
        if (package != null && package.IsSkipped)
        {
            package.IsSkipped = false;
            package.SkipReason = null;
            package.SkippedAt = null;
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
