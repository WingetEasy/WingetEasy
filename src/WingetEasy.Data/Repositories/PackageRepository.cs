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

    public async Task<IReadOnlyList<PackageSuppressionRule>> GetSuppressionRulesAsync(CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var entities = await _dbContext.Packages.AsNoTracking()
            .Where(p => p.IsSkipped || p.DeferredUntilUtc != null || p.IgnoredVersion != null)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities
            .Select(p => new PackageSuppressionRule(
                p.WingetId,
                p.Name,
                p.Source,
                p.IsSkipped,
                p.DeferredUntilUtc,
                p.IgnoredVersion,
                p.SkipReason,
                p.SkippedAt))
            .ToList();
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
                SkippedAt = DateTime.UtcNow,
                DeferredUntilUtc = null,
                IgnoredVersion = null
            };
            _dbContext.Packages.Add(package);
        }
        else
        {
            package.IsSkipped = true;
            package.SkipReason = reason;
            package.SkippedAt = DateTime.UtcNow;
            package.DeferredUntilUtc = null;
            package.IgnoredVersion = null;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeferPackageUntilAsync(string packageId, DateTime deferredUntilUtc, string? reason = null, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var package = await _dbContext.Packages.FirstOrDefaultAsync(p => p.WingetId == packageId, ct).ConfigureAwait(false);

        if (package == null)
        {
            package = new PackageEntity
            {
                WingetId = packageId,
                Name = packageId,
                Source = "winget",
                IsSkipped = false,
                DeferredUntilUtc = deferredUntilUtc,
                IgnoredVersion = null,
                SkipReason = reason,
                SkippedAt = DateTime.UtcNow
            };
            _dbContext.Packages.Add(package);
        }
        else
        {
            package.IsSkipped = false;
            package.DeferredUntilUtc = deferredUntilUtc;
            package.IgnoredVersion = null;
            package.SkipReason = reason;
            package.SkippedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task IgnorePackageVersionAsync(string packageId, string version, string? reason = null, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var package = await _dbContext.Packages.FirstOrDefaultAsync(p => p.WingetId == packageId, ct).ConfigureAwait(false);

        if (package == null)
        {
            package = new PackageEntity
            {
                WingetId = packageId,
                Name = packageId,
                Source = "winget",
                IsSkipped = false,
                DeferredUntilUtc = null,
                IgnoredVersion = version,
                SkipReason = reason,
                SkippedAt = DateTime.UtcNow
            };
            _dbContext.Packages.Add(package);
        }
        else
        {
            package.IsSkipped = false;
            package.DeferredUntilUtc = null;
            package.IgnoredVersion = version;
            package.SkipReason = reason;
            package.SkippedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UnskipPackageAsync(string packageId, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var package = await _dbContext.Packages.FirstOrDefaultAsync(p => p.WingetId == packageId, ct).ConfigureAwait(false);
        if (package != null && (package.IsSkipped || package.DeferredUntilUtc != null || package.IgnoredVersion != null))
        {
            package.IsSkipped = false;
            package.DeferredUntilUtc = null;
            package.IgnoredVersion = null;
            package.SkipReason = null;
            package.SkippedAt = null;
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
