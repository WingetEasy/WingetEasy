using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

public sealed class VelopackAppUpdateService : IAppUpdateService
{
    public const string DefaultUpdateSource = "https://github.com/WingetEasy/WingetEasy";

    private readonly IAppUpdateManager _manager;
    private readonly ILogger<VelopackAppUpdateService> _logger;

    public VelopackAppUpdateService(
        IAppUpdateManager manager,
        ILogger<VelopackAppUpdateService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersion { get; } =
        typeof(VelopackAppUpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task<AppUpdateInfo?> CheckForAppUpdateAsync(CancellationToken ct = default)
    {
        if (!IsInstalled)
        {
            return null;
        }

        return await _manager.CheckForUpdatesAsync(ct).ConfigureAwait(false);
    }

    public async Task DownloadAppUpdateAsync(AppUpdateInfo update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!IsInstalled)
        {
            return;
        }

        await _manager.DownloadUpdatesAsync(update, ct).ConfigureAwait(false);
    }

    public void ApplyUpdateAndRestart(AppUpdateInfo update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!IsInstalled)
        {
            return;
        }

        _manager.ApplyUpdatesAndRestart(update);
    }
}

public sealed class VelopackAppUpdateManager : IAppUpdateManager
{
    private readonly UpdateManager? _updateManager;

    public VelopackAppUpdateManager()
    {
        var source = Environment.GetEnvironmentVariable("WINGETEASY_UPDATE_SOURCE")
            ?? VelopackAppUpdateService.DefaultUpdateSource;
        _updateManager = new UpdateManager(source);
    }

    public bool IsInstalled => _updateManager?.IsInstalled == true;

    public async Task<AppUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct)
    {
        if (_updateManager is null || !IsInstalled)
        {
            return null;
        }

        var update = await _updateManager.CheckForUpdatesAsync().WaitAsync(ct).ConfigureAwait(false);
        return update?.TargetFullRelease is null
            ? null
            : new AppUpdateInfo(update.TargetFullRelease.Version.ToString());
    }

    public async Task DownloadUpdatesAsync(AppUpdateInfo update, CancellationToken ct)
    {
        if (_updateManager is null || !IsInstalled)
        {
            return;
        }

        var updateInfo = await _updateManager.CheckForUpdatesAsync().WaitAsync(ct).ConfigureAwait(false);
        if (updateInfo is null)
        {
            return;
        }

        await _updateManager.DownloadUpdatesAsync(updateInfo).WaitAsync(ct).ConfigureAwait(false);
    }

    public void ApplyUpdatesAndRestart(AppUpdateInfo update)
    {
        if (_updateManager is null || !IsInstalled)
        {
            return;
        }

        var pendingUpdate = _updateManager.UpdatePendingRestart;
        if (pendingUpdate is not null)
        {
            _updateManager.ApplyUpdatesAndRestart(pendingUpdate);
        }
    }
}
