using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

public class UpdateService : IUpdateService
{
    private readonly IWingetService _wingetService;
    private readonly IUpdateHistoryRepository _historyRepository;
    private readonly IAdminElevationHelper _adminElevationHelper;
    private readonly IRunningProcessService _runningProcessService;

    public UpdateService(
        IWingetService wingetService,
        IUpdateHistoryRepository historyRepository,
        IAdminElevationHelper adminElevationHelper,
        IRunningProcessService? runningProcessService = null)
    {
        _wingetService = wingetService;
        _historyRepository = historyRepository;
        _adminElevationHelper = adminElevationHelper;
        _runningProcessService = runningProcessService ?? new RunningProcessService();
    }

    public Task<IReadOnlyList<UpdateResult>> ExecuteUpdatesAsync(
        IEnumerable<string> packageIds,
        IProgress<(string name, int pct, int cur, int total)>? progress = null,
        Func<Task<bool>>? confirmElevation = null,
        CancellationToken ct = default)
    {
        return ExecuteUpdatesAsync(packageIds, progress, confirmElevation, null, isAutoUpdate: true, ct);
    }

    public async Task<IReadOnlyList<UpdateResult>> ExecuteUpdatesAsync(
        IEnumerable<string> packageIds,
        IProgress<(string name, int pct, int cur, int total)>? progress,
        Func<Task<bool>>? confirmElevation,
        Func<string, Task<AppRunningResolution>>? handleRunningApp,
        bool isAutoUpdate = false,
        CancellationToken ct = default,
        IProgress<(string packageId, string phase)>? phaseProgress = null)
    {
        var ids = packageIds.ToList();
        var results = new List<UpdateResult>(ids.Count);

        var elevationCandidates = new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            // Respeita o cancelamento se o utilizador clicar no botão "Cancelar" entre os pacotes
            ct.ThrowIfCancellationRequested();

            var currentId = ids[i];

            // Informa a UI que um novo pacote começou (0%)
            progress?.Report((currentId, 0, i + 1, ids.Count));

            // Cria um progress local que o IWingetService vai usar, e repassa para o global
            var pkgProgress = new Progress<int>(p =>
                progress?.Report((currentId, p, i + 1, ids.Count)));

            try
            {
                // 1. Verificação prévia de aplicativo em execução
                if (isAutoUpdate)
                {
                    // Nas atualizações automáticas em segundo plano (AutoUpdate), pular automaticamente
                    // qualquer aplicativo que esteja em execução com janela visível, adiando-o para a próxima verificação.
                    if (_runningProcessService.IsAppRunningWithVisibleWindow(currentId))
                    {
                        var skipResult = new UpdateResult(
                            currentId,
                            currentId,
                            false,
                            "Atualização adiada para a próxima verificação (aplicativo em execução com janela visível).",
                            TimeSpan.Zero,
                            ExitCode: WingetExitCodes.AppRunning,
                            IsApplicationRunning: true,
                            Skipped: true);

                        results.Add(skipResult);
                        continue;
                    }
                }
                else if (handleRunningApp != null && _runningProcessService.IsAppRunning(currentId))
                {
                    // Interativo: exibir aviso amigável
                    var resolution = await handleRunningApp(currentId).ConfigureAwait(false);
                    if (resolution == AppRunningResolution.Skip)
                    {
                        var skipResult = new UpdateResult(
                            currentId,
                            currentId,
                            false,
                            "Atualização pulada pelo usuário (aplicativo em uso).",
                            TimeSpan.Zero,
                            ExitCode: WingetExitCodes.AppRunning,
                            IsApplicationRunning: true,
                            Skipped: true);

                        results.Add(skipResult);
                        continue;
                    }

                    // Fechar aplicativo e continuar
                    await _runningProcessService.CloseAppProcessesAsync(currentId).ConfigureAwait(false);
                }

                // 2. Tentar instalar
IProgress<string>? pkgPhaseProgress = phaseProgress is not null
                    ? new SimpleProgress<string>(phase => phaseProgress.Report((currentId, phase)))
                    : null;
                var result = pkgPhaseProgress is not null
                    ? await _wingetService.InstallUpdateAsync(currentId, pkgProgress, ct, pkgPhaseProgress).ConfigureAwait(false)
                    : await _wingetService.InstallUpdateAsync(currentId, pkgProgress, ct).ConfigureAwait(false);

                // 3. Tratamento do código 0x8A150056 caso retorne do WinGet
                if (!result.Success && (result.ExitCode == WingetExitCodes.AppRunning || result.IsApplicationRunning))
                {
                    if (isAutoUpdate)
                    {
                        // AutoUpdate pula automaticamente
                        result = result with
                        {
                            Skipped = true,
                            IsApplicationRunning = true,
                            ErrorMessage = "Atualização adiada para a próxima verificação (aplicativo em execução)."
                        };
                    }
                    else if (handleRunningApp != null)
                    {
                        // Interativo: exibir aviso amigável se WinGet retornou 0x8A150056
                        var resolution = await handleRunningApp(result.PackageName).ConfigureAwait(false);
                        if (resolution == AppRunningResolution.CloseAndUpdate)
                        {
                            await _runningProcessService.CloseAppProcessesAsync(currentId).ConfigureAwait(false);
                            result = await _wingetService.InstallUpdateAsync(currentId, pkgProgress, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            result = result with
                            {
                                Skipped = true,
                                IsApplicationRunning = true,
                                ErrorMessage = "Atualização pulada pelo usuário (aplicativo em uso)."
                            };
                        }
                    }
                }

                results.Add(result);

                // Grava o resultado na base de dados SQLite apenas se não foi pulado por estar em execução
                if (!result.Skipped)
                {
                    if (RequiresElevation(result) && !_adminElevationHelper.IsRunningAsAdmin())
                    {
                        elevationCandidates.Add(currentId);
                    }
                    else
                    {
                        await _historyRepository.AddAsync(result, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Se a operação for cancelada pelo utilizador, interrompe o loop,
                // mas não perde o que já foi instalado e gravado com sucesso.
                throw;
            }
            catch (Exception ex)
            {
                // Falha num pacote NUNCA deve cancelar os restantes (Critério de Aceite)
                var failedResult = new UpdateResult(currentId, currentId, false, ex.Message, TimeSpan.Zero);
                results.Add(failedResult);

                await _historyRepository.AddAsync(failedResult, ct).ConfigureAwait(false);
            }
        }

        if (elevationCandidates.Count > 0)
        {
            var shouldElevate = confirmElevation is not null && await confirmElevation().ConfigureAwait(false);
            var elevatedResults = shouldElevate
                ? await _adminElevationHelper.ExecuteElevatedAsync(elevationCandidates, ct).ConfigureAwait(false)
                : elevationCandidates.Select(id => new UpdateResult(
                    id,
                    id,
                    false,
                    "Elevação de administrador cancelada pelo usuário.",
                    TimeSpan.Zero));

            foreach (var elevatedResult in elevatedResults)
            {
                var index = results.FindIndex(result => string.Equals(result.PackageId, elevatedResult.PackageId, StringComparison.OrdinalIgnoreCase));
                var mergedResult = elevatedResult;
                if (index >= 0)
                {
                    var original = results[index];
                    mergedResult = elevatedResult with
                    {
                        PackageName = string.IsNullOrWhiteSpace(original.PackageName) ? elevatedResult.PackageName : original.PackageName,
                        FromVersion = string.IsNullOrWhiteSpace(elevatedResult.FromVersion) ? original.FromVersion : elevatedResult.FromVersion,
                        ToVersion = string.IsNullOrWhiteSpace(elevatedResult.ToVersion) ? original.ToVersion : elevatedResult.ToVersion
                    };
                    results[index] = mergedResult;
                }
                else
                {
                    results.Add(mergedResult);
                }

                await _historyRepository.AddAsync(mergedResult, ct).ConfigureAwait(false);
            }
        }

        return results;
    }

    private sealed class SimpleProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }

    private static bool RequiresElevation(UpdateResult result)
    {
        if (result.Success || string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return false;
        }

        var error = result.ErrorMessage;
        return error.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || error.Contains("acesso negado", StringComparison.OrdinalIgnoreCase)
            || error.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || error.Contains("permiss", StringComparison.OrdinalIgnoreCase)
            || error.Contains("administrator", StringComparison.OrdinalIgnoreCase)
            || error.Contains("administrador", StringComparison.OrdinalIgnoreCase)
            || error.Contains("elevation", StringComparison.OrdinalIgnoreCase)
            || error.Contains("eleva", StringComparison.OrdinalIgnoreCase)
            || error.Contains("0x80070005", StringComparison.OrdinalIgnoreCase);
    }
}
