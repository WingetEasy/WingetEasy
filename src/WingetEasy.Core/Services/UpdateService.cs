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

    public UpdateService(IWingetService wingetService, IUpdateHistoryRepository historyRepository)
    {
        _wingetService = wingetService;
        _historyRepository = historyRepository;
    }

    public async Task<IReadOnlyList<UpdateResult>> ExecuteUpdatesAsync(
        IEnumerable<string> packageIds,
        IProgress<(string name, int pct, int cur, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var ids = packageIds.ToList();
        var results = new List<UpdateResult>(ids.Count);

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
                await _historyRepository.AddAsync(result, ct).ConfigureAwait(false);
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

        return results;
    }
}
