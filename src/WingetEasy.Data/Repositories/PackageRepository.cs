using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WingetEasy.Core.Interfaces;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class PackageRepository : IPackageRepository
{
    private readonly AppDbContext _dbContext;

    public PackageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<string>> GetSkippedIdsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Packages
            .Where(p => p.IsSkipped)
            .Select(p => p.WingetId)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task SkipPackageAsync(string packageId, string? reason = null, CancellationToken ct = default)
    {
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
