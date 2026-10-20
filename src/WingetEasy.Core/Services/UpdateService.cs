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

    public UpdateService(
        IWingetService wingetService,
        IUpdateHistoryRepository historyRepository,
        IAdminElevationHelper adminElevationHelper)
    {
        _wingetService = wingetService;
        _historyRepository = historyRepository;
        _adminElevationHelper = adminElevationHelper;
    }

    public async Task<IReadOnlyList<UpdateResult>> ExecuteUpdatesAsync(
        IEnumerable<string> packageIds,
        IProgress<(string name, int pct, int cur, int total)>? progress = null,
        Func<Task<bool>>? confirmElevation = null,
        CancellationToken ct = default)
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
                // Instala e aguarda (sequencialmente, para evitar travamentos nativos do Winget)
                var result = await _wingetService.InstallUpdateAsync(currentId, pkgProgress, ct).ConfigureAwait(false);
                results.Add(result);

                // Grava o resultado na base de dados SQLite
                if (RequiresElevation(result) && !_adminElevationHelper.IsRunningAsAdmin())
                {
                    elevationCandidates.Add(currentId);
                }
                else
                {
                    await _historyRepository.AddAsync(result, ct).ConfigureAwait(false);
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
                var index = results.FindIndex(result => result.PackageId == elevatedResult.PackageId);
                if (index >= 0)
                {
                    results[index] = elevatedResult;
                }
                else
                {
                    results.Add(elevatedResult);
                }

                await _historyRepository.AddAsync(elevatedResult, ct).ConfigureAwait(false);
            }
        }

        return results;
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
