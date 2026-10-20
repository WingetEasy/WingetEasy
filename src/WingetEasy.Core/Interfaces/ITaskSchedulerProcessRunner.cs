using System.Threading;
using System.Threading.Tasks;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Executa um comando externo do sistema operacional.
/// </summary>
public interface ITaskSchedulerProcessRunner
{
    Task<int> RunAsync(string fileName, string arguments, CancellationToken ct = default);
}
