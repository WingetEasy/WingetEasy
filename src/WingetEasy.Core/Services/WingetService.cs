using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

/// <summary>
/// Implementação concreta do serviço de interação com o Windows Package Manager (Winget).
/// Responsável por executar comandos via CLI, gerir processos do sistema, evitar deadlocks de I/O
/// e fazer o parsing robusto do output JSON devolvido pelo terminal.
/// </summary>

public class WingetService : IWingetService
{
    private static readonly Regex PkgIdRegex = new(@"^[A-Za-z0-9._\-]+$", RegexOptions.Compiled);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    private readonly IPackageRepository _packageRepository;
    private readonly ILogger<WingetService> _logger;

    // controle do Cache Interno
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private DateTime _cacheExpiryTime = DateTime.MinValue;
    private IEnumerable<WingetPackage> _cachedPackages = [];

    /// <summary>
    /// Inicializa uma nova instância de <see cref="WingetService"/>.
    /// </summary>
    /// <param name="packageRepository">Repositório para consultar regras de pacotes (ex: ignorados).</param>
    /// <param name="logger">Serviço de logging estruturado.</param>

    public WingetService(IPackageRepository packageRepository, ILogger<WingetService> logger)
    {
        _packageRepository = packageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Procura por atualizações de software disponíveis utilizando o 'winget upgrade'.
    /// Utiliza cache em memória de 30 minutos para evitar chamadas excessivas e lentas ao terminal.
    /// </summary>
    /// <param name="ct">Token de cancelamento da operação.</param>
    /// <returns>Coleção imutável de pacotes que possuem atualizações pendentes e não estão ignorados.</returns>

    public async Task<IEnumerable<WingetPackage>> CheckUpdatesAsync(CancellationToken ct = default)
    {
        // Bloqueio assíncrono para evitar chamadas duplas simultâneas e proteger o cache
        await _cacheLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Retorna o cache se ainda estiver válido e não estiver vazio
            if (DateTime.UtcNow < _cacheExpiryTime && _cachedPackages.Any())
            {
                _logger.LogInformation("Retornando atualizações do cache interno.");
                return _cachedPackages;
            }

            // Cria um timeout local isolado, mas interligado ao cancelamento global da aplicação
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(CheckTimeout);

            try
            {
                // Executa o processo de forma silenciosa e não-interativa
                var rawJson = await ExecuteWingetCommandAsync("upgrade --include-unknown --output json --disable-interactivity --accept-source-agreements", timeoutCts.Token).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(rawJson))
                    return [];

                var packages = ParseWingetJson(rawJson);

                // Filtra os pacotes que o utilizador escolheu ignorar
                var skippedIds = await _packageRepository.GetSkippedIdsAsync(timeoutCts.Token).ConfigureAwait(false);
                var skippedSet = new HashSet<string>(skippedIds, StringComparer.OrdinalIgnoreCase);

                var finalPackages = packages.Where(p => !skippedSet.Contains(p.Id)).ToList();

                // Atualiza o estado do cache com o tempo atual + tempo de expiração
                _cachedPackages = finalPackages;
                _cacheExpiryTime = DateTime.UtcNow.Add(CacheExpiry);

                return finalPackages;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("O processo do Winget excedeu o timeout de {Minutos} minutos.", CheckTimeout.TotalMinutes);
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao procurar atualizações via Winget.");
                return [];
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Processa o JSON bruto devolvido pelo Winget, extraindo os metadados dos pacotes.
    /// Lida com falhas estruturais e "sujeira" no terminal (ex: barras de progresso que antecedem o JSON).
    /// </summary>

    private IEnumerable<WingetPackage> ParseWingetJson(string raw)
    {
        // O Winget por vezes escreve texto de carregamento antes do JSON.
        // Procurar o primeiro '{' garante que só fazemos parse ao JSON válido.
        var start = raw.IndexOf('{');
        if (start < 0) return [];

        try
        {
            using var doc = JsonDocument.Parse(raw[start..]);
            if (!doc.RootElement.TryGetProperty("Sources", out var sourcesElement))
                return [];

            var results = new List<WingetPackage>();

            foreach (var source in sourcesElement.EnumerateArray())
            {
                if (!source.TryGetProperty("Packages", out var packagesElement)) continue;

                var sourceName = source.TryGetProperty("Source", out var sn) ? sn.GetString() : "winget";

                foreach (var package in packagesElement.EnumerateArray())
                {
                    var id = package.GetProperty("Id").GetString() ?? "";

                    if (!PkgIdRegex.IsMatch(id)) continue;

                    var name = package.GetProperty("Name").GetString() ?? id;
                    var availableVer = package.GetProperty("AvailableVersion").GetString() ?? "";
                    var currentVer = package.TryGetProperty("InstalledVersion", out var iv) ? iv.GetString() ?? "" : "";

                    results.Add(new WingetPackage(
                        Id: id,
                        Name: name,
                        CurrentVersion: currentVer,
                        AvailableVersion: availableVer,
                        Source: sourceName
                    ));
                }
            }
            return results;
        }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Falha ao fazer parse do output JSON do Winget.");
                return [];
            }
    }

    /// <summary>
    /// Executa um comando no terminal do Windows, lendo a saída padrão e a saída de erro simultaneamente.
    /// </summary>


    private async Task<string> ExecuteWingetCommandAsync(string arguments, CancellationToken ct)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8 // Lida com acentuação
        };

        using var process = new Process {StartInfo = processStartInfo };

        try
        {
            process.Start();

        }
        catch (System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning("O executável 'winget' não foi encontrado. O Windows Package Manager está instalado?");
            return string.Empty;// Retorna lista vazia se winget não existir
        }

        // Garante que o processo filho morre se houver Timeout
        await using var processKiller = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); }
            catch { /* Ignora erros ao tentar matar o processo */ }
        }).ConfigureAwait(false);

        // A leitura de StandardOutput e StandardError tem de ser feita sem 'await' imediato,
        // caso contrário buffers cheios do Windows podem causar Deadlocks na thread nativa.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return await stdoutTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // Repassa o timeout para cima
        }
    }

    // --- Stubs para satisfazer a IWingetService por agora (Serão feitos noutras issues) ---
    public Task<UpdateResult> InstallUpdateAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<WingetPackage>> GetInstalledPackagesAsync(CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<bool> IsWingetAvailableAsync()
        => throw new NotImplementedException();

    public Task<string> GetWingetVersionAsync()
        => throw new NotImplementedException();


}
