using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

public interface IAppUpdateManager
{
    bool IsInstalled { get; }
    Task<AppUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct);
    Task DownloadUpdatesAsync(AppUpdateInfo update, CancellationToken ct);
    void ApplyUpdatesAndRestart(AppUpdateInfo update);
}
