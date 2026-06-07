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

    // Regex para extrair a versão no formato v1.x.x
    private static readonly Regex VersionRegex = new(@"v\d+\.\d+\.\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    // Tempos de cache específicos
    private static readonly TimeSpan InstalledCacheExpiry = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AvailableCacheExpiry = TimeSpan.FromHours(1);

    private readonly IPackageRepository _packageRepository;
    private readonly ILogger<WingetService> _logger;


    // controle do Cache Interno
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private DateTime _cacheExpiryTime = DateTime.MinValue;
    private IEnumerable<WingetPackage> _cachedPackages = [];

    // Controles de Cache para Pacotes Instalados
    private readonly SemaphoreSlim _installedCacheLock = new(1, 1);
    private DateTime _installedCacheExpiryTime = DateTime.MinValue;
    private IEnumerable<WingetPackage> _cachedInstalledPackages = [];

    //Controles de Cache para Disponibilidade e Versão
    private DateTime _availableCacheExpiryTime = DateTime.MinValue;
    private bool? _cachedIsAvailable = null;
    private string? _cachedVersion = null;

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

                var packages = ParseWingetJsonInternal(rawJson);

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

    internal IEnumerable<WingetPackage> ParseWingetJsonInternal(string raw)
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
                    var availableVer = package.TryGetProperty("AvailableVersion", out var av) ? av.GetString() ?? "" : "";
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

    // Método extraído para ser testável isoladamente e garantir que a validação do ID é consistente em toda a aplicação
    internal static void ValidatePackageId(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !PkgIdRegex.IsMatch(packageId))
        {
            throw new ArgumentException($"Package ID inválido ou com caracteres não permitidos: {packageId}", nameof(packageId));
        }
    }

    // --- Stubs para satisfazer a IWingetService por agora (Serão feitos noutras issues) ---
    public async Task<UpdateResult> InstallUpdateAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            // 1. Validação estrita do ID para evitar injeção de comandos no terminal
            ValidatePackageId(packageId);

            var sw = Stopwatch.StartNew();
            progress?.Report(0); // Inicio a 0%

            // Timeout de 10 minutos associado ao token do utilizador
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(10));

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"upgrade --id {packageId} --silent --accept-source-agreements --accept-package-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = processStartInfo };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                sw.Stop();
                return new UpdateResult(packageId, packageId, false, "O executável 'winget' não foi encontrado.", sw.Elapsed);
            }

            // Garante que o processo morre se a operação for cancelada (pelo timeout ou utilizador)
            await using var processKiller = timeoutCts.Token.Register(() =>
            {

                try { if (!process.HasExited) process.Kill(true); } catch { }
            }).ConfigureAwait(false);

            var errorLines = new List<string>();
            var lastOutputLines = new Queue<string>(3);

            // Task para ler o StdOut linha a linha e emitir o progresso
            var stdoutTask = Task.Run(async () =>
            {
                try
                {
                    using var reader = process.StandardOutput;
                    while (await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false) is { } line)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        lastOutputLines.Enqueue(line);
                        if (lastOutputLines.Count > 3) lastOutputLines.Dequeue();

                        // Lógica simples de progresso lendo as palavras-chave do winget
                        if (line.Contains("Downloading", StringComparison.OrdinalIgnoreCase) || line.Contains("Baixando", StringComparison.OrdinalIgnoreCase))
                            progress?.Report(25);
                        else if (line.Contains("Installing", StringComparison.OrdinalIgnoreCase) || line.Contains("Instalando", StringComparison.OrdinalIgnoreCase))
                            progress?.Report(75);

                    }
                }
                catch (OperationCanceledException) { /* Ignorado na leitura */ }
            });

            // Task para capturar erros e evitar deadlocks
            var stderrTask = Task.Run(async () =>
            {
                try
                {
                    using var reader = process.StandardError;
                    while (await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false) is { } line)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            errorLines.Add(line);
                    }
                }
                catch (OperationCanceledException) { /* Ignorado na leitura */ }
            });

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                var isTimeout = timeoutCts.Token.IsCancellationRequested && !ct.IsCancellationRequested;
                var cancelReason = isTimeout ? "Tempo limite de 10 minutos excedido." : "Instalação cancelada pelo utilizador.";
                return new UpdateResult(packageId, packageId, false, cancelReason, sw.Elapsed);
            }

            sw.Stop();
            progress?.Report(100); //Fim a 100%

            int exitCode = process.ExitCode;
            // 0 = Sucesso | -1978335189 = Não há atualizações aplicáveis (já atualizado)
            bool success = exitCode == 0 || exitCode == -1978335189;

            if (success)
            {
                _installedCacheExpiryTime = DateTime.MinValue;
            }

            string? errorMessage = null;
            if (!success)
            {
                errorMessage = $"Código: {exitCode}. " + string.Join(" | ", errorLines);
                if (string.IsNullOrWhiteSpace(errorMessage) || errorMessage.EndsWith(". "))
                {
                    // Se não houver erro no stderr, usamos as últimas linhas do stdout como dica
                    errorMessage += string.Join(" | ", lastOutputLines);
                }
            }

            return new UpdateResult(packageId, packageId, success, errorMessage, sw.Elapsed);
        }

    public async Task<IEnumerable<WingetPackage>> GetInstalledPackagesAsync(CancellationToken ct = default)
        {
            await _installedCacheLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (DateTime.UtcNow < _installedCacheExpiryTime && _cachedInstalledPackages.Any())
                    return _cachedInstalledPackages;

                using var timeoutCts  = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(CheckTimeout);

                try
                {
                    var rawJson = await ExecuteWingetCommandAsync("list --output json --disable-interactivity --accept-source-agreements", timeoutCts.Token).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(rawJson)) return [];

                    var packages = ParseWingetJsonInternal(rawJson);

                    _cachedInstalledPackages = packages.ToList();
                    _installedCacheExpiryTime = DateTime.UtcNow.Add(InstalledCacheExpiry);

                    return _cachedInstalledPackages;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro inesperado ao listar pacotes instalados via Winget.");
                    return [];
                }

            }
            finally
            {
                _installedCacheLock.Release();
            }
        }

    public async Task<bool> IsWingetAvailableAsync()
        {
            if (DateTime.UtcNow < _availableCacheExpiryTime && _cachedIsAvailable.HasValue)
                return _cachedIsAvailable.Value;

            await CheckWingetVersionInternalAsync().ConfigureAwait(false);
            return _cachedIsAvailable ?? false;

        }

    public async Task<string> GetWingetVersionAsync()
        {
            if (DateTime.UtcNow < _availableCacheExpiryTime && _cachedVersion != null)
                return _cachedVersion;

            await CheckWingetVersionInternalAsync().ConfigureAwait(false);
            return _cachedVersion ?? string.Empty;
        }

    private async Task CheckWingetVersionInternalAsync()
    {
        try
        {
            var output = await ExecuteWingetCommandAsync("--version", CancellationToken.None).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(output))
            {
                _cachedIsAvailable = true; // Winget existe mas não devolveu versão (raro, mas possível)
                var match = VersionRegex.Match(output);
                _cachedVersion = match.Success ? match.Value : output.Trim();
                _availableCacheExpiryTime = DateTime.UtcNow.Add(AvailableCacheExpiry);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao verificar a versão do Winget. Provavelmente não está instalado.");
        }

        // Se falhou sem dar throw ou retornou vazio, marca como false
        _cachedIsAvailable = false;
        _cachedVersion = string.Empty;
        _availableCacheExpiryTime = DateTime.UtcNow.Add(AvailableCacheExpiry);
    }




}
