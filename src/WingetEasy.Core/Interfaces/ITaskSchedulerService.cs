using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Gerencia a tarefa nativa do Windows responsável pelas verificações automáticas.
/// </summary>
public interface ITaskSchedulerService
{
    Task CreateOrUpdateTaskAsync(ScheduleConfig config, CancellationToken ct = default);
    Task<bool> TaskExistsAsync(CancellationToken ct = default);
    Task DeleteTaskAsync(CancellationToken ct = default);
}
