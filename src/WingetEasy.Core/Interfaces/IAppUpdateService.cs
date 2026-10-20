using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

public interface IAppUpdateService
{
    bool IsInstalled { get; }
    string CurrentVersion { get; }
    Task<AppUpdateInfo?> CheckForAppUpdateAsync(CancellationToken ct = default);
    Task DownloadAppUpdateAsync(AppUpdateInfo update, CancellationToken ct = default);
    void ApplyUpdateAndRestart(AppUpdateInfo update);
}
