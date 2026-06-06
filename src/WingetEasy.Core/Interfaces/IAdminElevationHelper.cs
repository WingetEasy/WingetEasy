using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Isolador de segurança. Garante que o processo principal do WingetEasy corra com permissões normais,
/// solicitando elevação de privilégios (UAC) apenas num processo filho durante o momento da instalação.
/// </summary>

public interface IAdminElevationHelper
{
    bool IsRunningAsAdmin();
    Task <IEnumerable<UpdateResult>> ExecuteElevatedAsync(IEnumerable<string> packageIds, CancellationToken ct = default);
}
