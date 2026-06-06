using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

public class AdminElevationHelper : IAdminElevationHelper
{
    public bool IsRunningAsAdmin()
    {
#pragma warning disable CA1416 // Ignora o aviso de plataforma (sabemos que vai rodar no Windows)
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
    }

    public async Task<IEnumerable<UpdateResult>> ExecuteElevatedAsync(IEnumerable<string> packageIds, CancellationToken ct = default)
    {
        // Validação de segurança com Regex
        var validIds = packageIds.Where(id => Regex.IsMatch(id, @"^[a-zA-Z0-9\.\-_]+$")).ToList();
        if (validIds.Count == 0) return [];

        var jobId = Guid.NewGuid().ToString();
        var tempDir = Path.GetTempPath();
        var jobFile = Path.Combine(tempDir, $"winget_easy_job_{jobId}.json");
        var resultFile = Path.Combine(tempDir, $"winget_easy_result_{jobId}.json");

        try
        {
            // Grava o ficheiro com a lista de pacotes
            var job = new ElevatedJob { JobId = jobId, PackageIds = validIds };
            await File.WriteAllTextAsync(jobFile, JsonSerializer.Serialize(job), ct).ConfigureAwait(false);

            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
                throw new InvalidOperationException("Não foi possível determinar o executável.");

            // Dispara processo elevado (UAC)
            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = $"--elevated --job \"{jobFile}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Usuário clicou em "Não" no UAC. Retornar sem crashar.
                return validIds.Select(id => new UpdateResult(id, id, false, "Operação cancelada pelo utilizador (UAC).", TimeSpan.Zero));

            }

            // Aguarda o resultado via FileSystemWatcher
            using var tcs = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, tcs.Token);
            var completionSource = new TaskCompletionSource<bool>();

            using var watcher = new FileSystemWatcher(tempDir,$"winget_easy_result_{jobId}.json")
            {
                EnableRaisingEvents = true,
            };
            watcher.Created += (s, e) => completionSource.TrySetResult(true);
            watcher.Changed += (s, e) => completionSource.TrySetResult(true);

            if (File.Exists(resultFile)) completionSource.TrySetResult(true); // Prevenção caso crie muito rápido

            linkedCts.CancelAfter(TimeSpan.FromMinutes(30)); // Timeout de segurança

            try
            {
                await using (linkedCts.Token.Register(() => completionSource.TrySetCanceled()))
                {
                    await completionSource.Task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (!File.Exists(resultFile))
                    return validIds.Select(id => new UpdateResult(id, id, false, "Timeout enquanto espera o processo elevado.", TimeSpan.Zero));
            }

            await Task.Delay(500, ct).ConfigureAwait(false); // Tempo para o Windows libertar o lock do ficheiro

            if (File.Exists(resultFile))
            {
                var resultJson = await File.ReadAllTextAsync(resultFile, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<List<UpdateResult>>(resultJson) ?? [];
            }

            return validIds.Select(id => new UpdateResult(id, id, false, "Ficheiro de resultado não encontrado.", TimeSpan.Zero));
        }
        finally
        {
            // Garantir que os arquivos temporários são deletados sempre
            try {if (File.Exists(jobFile)) File.Delete(jobFile); } catch { /* Ignorar erros de limpeza */ }
            try { if (File.Exists(resultFile)) File.Delete(resultFile); } catch { /* Ignorar erros de limpeza */ }
        }
    }

}
