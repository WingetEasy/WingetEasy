using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
/// e fazer o parsing robusto do output devolvido pelo terminal.
/// </summary>
public partial class WingetService : IWingetService
{
    [GeneratedRegex(@"^[A-Za-z0-9._\-+]+$")]
    private static partial Regex GetPkgIdRegex();
    private static Regex PkgIdRegex => GetPkgIdRegex();

    [GeneratedRegex(@"\S+")]
    private static partial Regex GetNonWhitespaceRegex();
    private static Regex NonWhitespaceRegex => GetNonWhitespaceRegex();

    [GeneratedRegex(@"^\d+\s+(atualiza\S*|upgrades?\b|pacotes?\b|packages?\b)", RegexOptions.IgnoreCase)]
    private static partial Regex GetFooterLineRegex();
    private static Regex FooterLineRegex => GetFooterLineRegex();

    // Regex para extrair a versão no formato v1.x.x
    [GeneratedRegex(@"v\d+\.\d+\.\d+", RegexOptions.IgnoreCase)]
    private static partial Regex GetVersionRegex();
    private static Regex VersionRegex => GetVersionRegex();

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    // Tempos de cache específicos
    private static readonly TimeSpan InstalledCacheExpiry = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AvailableCacheExpiry = TimeSpan.FromHours(1);

    private readonly IPackageRepository _packageRepository;
    private readonly ILogger<WingetService> _logger;
    private readonly IAdminElevationHelper? _adminElevationHelper;
    private readonly Func<string, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>>? _commandRunner;
    private readonly Func<CancellationToken, Task<bool>>? _elevatedRepairRunner;

    // controle do Cache Interno
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private DateTime _cacheExpiryTime = DateTime.MinValue;
    private IEnumerable<WingetPackage> _cachedPackages = [];

    // Controles de Cache para Pacotes Instalados
    private readonly SemaphoreSlim _installedCacheLock = new(1, 1);
    private DateTime _installedCacheExpiryTime = DateTime.MinValue;
    private IEnumerable<WingetPackage> _cachedInstalledPackages = [];

    // Controles de Cache para Disponibilidade e Versão
    private DateTime _availableCacheExpiryTime = DateTime.MinValue;
    private bool? _cachedIsAvailable = null;
    private string? _cachedVersion = null;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="WingetService"/>.
    /// </summary>
    /// <param name="packageRepository">Repositório para consultar regras de pacotes (ex: ignorados).</param>
    /// <param name="logger">Serviço de logging estruturado.</param>
    /// <param name="adminElevationHelper">Auxiliar opcional de verificação/elevação administrativa.</param>
    public WingetService(
        IPackageRepository packageRepository,
        ILogger<WingetService> logger,
        IAdminElevationHelper? adminElevationHelper = null)
    {
        _packageRepository = packageRepository;
        _logger = logger;
        _adminElevationHelper = adminElevationHelper;
    }

    internal WingetService(
        IPackageRepository packageRepository,
        ILogger<WingetService> logger,
        Func<string, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> commandRunner,
        IAdminElevationHelper? adminElevationHelper = null,
        Func<CancellationToken, Task<bool>>? elevatedRepairRunner = null)
        : this(packageRepository, logger, adminElevationHelper)
    {
        _commandRunner = commandRunner;
        _elevatedRepairRunner = elevatedRepairRunner;
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
                var rawOutput = await ExecuteWingetCommandAsync(
                    "upgrade --accept-source-agreements --disable-interactivity",
                    timeoutCts.Token).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(rawOutput))
                    return [];

                var packages = ParseWingetOutputInternal(rawOutput);

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
    /// Processa a saída do Winget (JSON ou tabela CLI em qualquer idioma, como PT-BR e EN-US),
    /// extraindo os metadados dos pacotes e ignorando linhas de progresso no terminal.
    /// </summary>
    internal IEnumerable<WingetPackage> ParseWingetOutputInternal(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        if (raw.Contains("\"Sources\"", StringComparison.OrdinalIgnoreCase) || raw.TrimStart().StartsWith('{'))
        {
            return ParseWingetJsonOnly(raw);
        }

        return ParseWingetTableInternal(raw);
    }

    /// <summary>
    /// Processa o output devolvido pelo Winget (JSON ou tabela CLI), extraindo os metadados dos pacotes.
    /// Lida com falhas estruturais e "sujeira" no terminal (ex: barras de progresso que antecedem o output).
    /// </summary>
    internal IEnumerable<WingetPackage> ParseWingetJsonInternal(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var start = raw.IndexOf('{');
        if (start >= 0)
        {
            return ParseWingetJsonOnly(raw);
        }

        return ParseWingetTableInternal(raw);
    }

    private IEnumerable<WingetPackage> ParseWingetJsonOnly(string raw)
    {
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
    /// Faz o parsing da tabela textual do 'winget upgrade' (independente do idioma: PT-BR, EN-US, etc.).
    /// Localiza a linha separadora de hifens ('---...---') e o cabeçalho imediatamente acima,
    /// ignora caracteres de progresso ('-', '\', '|', '/', '\r') e extrai cada pacote até o rodapé.
    /// </summary>
    internal IEnumerable<WingetPackage> ParseWingetTableInternal(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        // Divide por '\n' e limpa os frames de progresso separados por '\r' na mesma linha
        var rawLines = raw.Split('\n');
        var lines = new List<string>(rawLines.Length);

        foreach (var rawLine in rawLines)
        {
            var segments = rawLine.Split('\r', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            // Escolhe o último segmento útil (descartando frames do spinner como "-", "\", "|", "/")
            string chosen = string.Empty;
            for (int s = segments.Length - 1; s >= 0; s--)
            {
                var candidate = segments[s].TrimEnd();
                var trimmed = candidate.Trim();
                if (trimmed.Length == 0 || IsSpinnerFrame(trimmed))
                    continue;

                chosen = candidate;
                break;
            }

            lines.Add(chosen);
        }

        // Localiza a primeira linha separadora de hifens (---...---) que possua cabeçalho acima
        int separatorIndex = -1;
        string headerLine = string.Empty;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length >= 10 && trimmed.All(c => c == '-'))
            {
                // Procura a linha imediatamente acima que não seja vazia nem spinner
                for (int h = i - 1; h >= 0; h--)
                {
                    var candidateHeader = lines[h].Trim();
                    if (!string.IsNullOrEmpty(candidateHeader) && !IsSpinnerFrame(candidateHeader))
                    {
                        headerLine = lines[h];
                        separatorIndex = i;
                        break;
                    }
                }

                if (separatorIndex >= 0)
                    break;
            }
        }

        if (separatorIndex < 0)
            return [];

        // Extrai offsets das colunas do cabeçalho como apoio
        var headerMatches = NonWhitespaceRegex.Matches(headerLine);
        int[]? colOffsets = headerMatches.Count >= 5
            ? headerMatches.Select(m => m.Index).Take(5).ToArray()
            : null;

        var results = new List<WingetPackage>();

        for (int i = separatorIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
                break;

            if (IsSpinnerFrame(trimmed))
                continue;

            // Para ao encontrar outra linha separadora ou linhas de rodapé (ex: "X atualizações disponíveis.")
            if ((trimmed.Length >= 10 && trimmed.All(c => c == '-')) || IsFooterLine(trimmed))
                break;

            if (TryParsePackageRow(line, colOffsets, out var package) && package is not null)
            {
                results.Add(package);
            }
        }

        return results;
    }

    private static bool IsSpinnerFrame(string trimmed)
    {
        return trimmed is "-" or "\\" or "|" or "/";
    }

    private static bool IsFooterLine(string trimmed)
    {
        if (FooterLineRegex.IsMatch(trimmed))
            return true;

        if (trimmed.EndsWith('.'))
            return true;

        return trimmed.Contains("atualizações disponíveis", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("atualização disponível", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("upgrades available", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("upgrade available", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("segmentação explícita", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("direcionamento explícito", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("explicit targeting", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePackageRow(string line, int[]? colOffsets, out WingetPackage? package)
    {
        package = null;
        var row = line.TrimEnd();
        var tokens = NonWhitespaceRegex.Matches(row);

        // 1. Parsing reverso a partir do final da linha: Source -> Available -> Version -> Id -> Name
        if (tokens.Count >= 5)
        {
            var source = tokens[^1].Value;
            var availableVer = tokens[^2].Value;
            int verIndex = tokens.Count - 3;
            var currentVer = tokens[verIndex].Value;

            // Lida com versões precedidas por '<' ou '>' (ex: "< 174.2.0.13389")
            if (verIndex - 1 >= 2 && tokens[verIndex - 1].Value is "<" or ">" or "<=" or ">=")
            {
                verIndex--;
                currentVer = $"{tokens[verIndex].Value} {tokens[verIndex + 1].Value}";
            }

            int idIndex = verIndex - 1;
            if (idIndex >= 1)
            {
                var idMatch = tokens[idIndex];
                var rawId = idMatch.Value.TrimEnd('…');
                var name = row[..idMatch.Index].Trim();

                if (!string.IsNullOrWhiteSpace(name) && PkgIdRegex.IsMatch(rawId))
                {
                    package = new WingetPackage(
                        Id: rawId,
                        Name: name,
                        CurrentVersion: currentVer,
                        AvailableVersion: availableVer,
                        Source: source);
                    return true;
                }
            }
        }

        // 2. Fallback por offsets das colunas do cabeçalho
        if (colOffsets is { Length: 5 } && row.Length > colOffsets[3])
        {
            string Slice(int start, int end)
            {
                if (start >= row.Length) return string.Empty;
                var actualEnd = Math.Min(end, row.Length);
                return actualEnd > start ? row[start..actualEnd].Trim() : string.Empty;
            }

            var name = Slice(colOffsets[0], colOffsets[1]);
            var rawId = Slice(colOffsets[1], colOffsets[2]).TrimEnd('…');
            var currentVer = Slice(colOffsets[2], colOffsets[3]);
            var availableVer = colOffsets[4] < row.Length
                ? Slice(colOffsets[3], colOffsets[4])
                : Slice(colOffsets[3], row.Length);
            var source = colOffsets[4] < row.Length
                ? Slice(colOffsets[4], row.Length)
                : "winget";

            if (!string.IsNullOrWhiteSpace(name) && PkgIdRegex.IsMatch(rawId))
            {
                package = new WingetPackage(
                    Id: rawId,
                    Name: name,
                    CurrentVersion: currentVer,
                    AvailableVersion: availableVer,
                    Source: string.IsNullOrWhiteSpace(source) ? "winget" : source);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Executa um comando no terminal do Windows, lendo a saída padrão e a saída de erro simultaneamente.
    /// </summary>
    private async Task<string> ExecuteWingetCommandAsync(string arguments, CancellationToken ct)
    {
        if (_commandRunner != null)
        {
            var result = await _commandRunner(arguments, ct).ConfigureAwait(false);
            return result.StdOut;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = processStartInfo };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning("O executável 'winget' não foi encontrado. O Windows Package Manager está instalado?");
            return string.Empty;
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
            _ = await stderrTask.ConfigureAwait(false);
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

    public Task<UpdateResult> InstallUpdateAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default) =>
        RunInstallCommandAsync("upgrade", packageId, progress, ct);

    /// <summary>
    /// Instala um pacote que pode ainda não existir na máquina (winget install), ao contrário de InstallUpdateAsync (winget upgrade).
    /// </summary>
    public Task<UpdateResult> InstallPackageAsync(string packageId, IProgress<int>? progress = null, CancellationToken ct = default) =>
        RunInstallCommandAsync("install", packageId, progress, ct);

    private async Task<UpdateResult> RunInstallCommandAsync(string verb, string packageId, IProgress<int>? progress, CancellationToken ct)
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
            Arguments = $"{verb} --id {packageId} --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
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

        int exitCode = process.ExitCode;
        // 0 = Sucesso | -1978335189 = Não há atualizações aplicáveis (já atualizado)
        // -1978335135 = Pacote já instalado (winget install)
        bool success = exitCode == 0 || exitCode == -1978335189 || (verb == "install" && exitCode == -1978335135);
        progress?.Report(success ? 100 : -1);

        if (success)
        {
            _installedCacheExpiryTime = DateTime.MinValue;
            _cacheExpiryTime = DateTime.MinValue;
        }

        bool isAppRunning = exitCode == WingetExitCodes.AppRunning
            || errorLines.Any(l => l.Contains("0x8a150056", StringComparison.OrdinalIgnoreCase))
            || lastOutputLines.Any(l => l.Contains("0x8a150056", StringComparison.OrdinalIgnoreCase));

        string? errorMessage = null;
        if (!success)
        {
            if (isAppRunning)
            {
                errorMessage = "O aplicativo está aberto no momento (código 0x8A150056).";
            }
            else
            {
                errorMessage = $"Código: {exitCode}. " + string.Join(" | ", errorLines);
                if (string.IsNullOrWhiteSpace(errorMessage) || errorMessage.EndsWith(". "))
                {
                    // Se não houver erro no stderr, usamos as últimas linhas do stdout como dica
                    errorMessage += string.Join(" | ", lastOutputLines);
                }
            }
        }

        var cachedPkg = _cachedPackages.FirstOrDefault(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
        return new UpdateResult(
            packageId,
            cachedPkg?.Name ?? packageId,
            success,
            errorMessage,
            sw.Elapsed,
            cachedPkg?.CurrentVersion ?? string.Empty,
            cachedPkg?.AvailableVersion ?? string.Empty,
            DateTime.UtcNow,
            ExitCode: exitCode,
            IsApplicationRunning: isAppRunning);
    }

    public async Task<IEnumerable<WingetPackage>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        await _installedCacheLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (DateTime.UtcNow < _installedCacheExpiryTime && _cachedInstalledPackages.Any())
                return _cachedInstalledPackages;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(CheckTimeout);

            try
            {
                var rawOutput = await ExecuteWingetCommandAsync("list --accept-source-agreements --disable-interactivity", timeoutCts.Token).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(rawOutput)) return [];

                var packages = ParseWingetOutputInternal(rawOutput);

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
                _cachedIsAvailable = true;
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

        _cachedIsAvailable = false;
        _cachedVersion = string.Empty;
        _availableCacheExpiryTime = DateTime.UtcNow.Add(AvailableCacheExpiry);
    }

    /// <summary>
    /// Repara e atualiza o catálogo de fontes locais do Winget executando
    /// 'winget source reset --force' seguido de 'winget source update'.
    /// Caso o processo não esteja elevado e falhe por falta de permissão,
    /// aciona o fluxo de elevação ou retorna falha tratada.
    /// </summary>
    public async Task<bool> RepairSourcesAsync(CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CheckTimeout);

        try
        {
            var resetResult = await ExecuteWingetWithResultAsync("source reset --force", timeoutCts.Token).ConfigureAwait(false);
            if (resetResult.ExitCode != 0)
            {
                var isAdmin = _adminElevationHelper?.IsRunningAsAdmin() ?? false;
                if (!isAdmin && IsPermissionFailure(resetResult.ExitCode, resetResult.StdOut, resetResult.StdErr))
                {
                    _logger.LogInformation("Reparo de fontes exigiu elevação de privilégios; iniciando fluxo elevado.");
                    var elevatedSuccess = await ExecuteElevatedRepairSourcesAsync(timeoutCts.Token).ConfigureAwait(false);
                    if (elevatedSuccess)
                    {
                        InvalidateCaches();
                        _logger.LogInformation("Fontes do Winget reparadas e atualizadas com sucesso via elevação.");
                        return true;
                    }

                    _logger.LogWarning("Não foi possível concluir o reparo elevado das fontes do Winget.");
                    return false;
                }

                _logger.LogWarning(
                    "Falha ao executar 'winget source reset --force' (ExitCode: {ExitCode}). Erro: {Error}",
                    resetResult.ExitCode,
                    string.IsNullOrWhiteSpace(resetResult.StdErr) ? resetResult.StdOut : resetResult.StdErr);
                return false;
            }

            var updateResult = await ExecuteWingetWithResultAsync("source update", timeoutCts.Token).ConfigureAwait(false);
            if (updateResult.ExitCode != 0)
            {
                _logger.LogWarning(
                    "Falha ao executar 'winget source update' (ExitCode: {ExitCode}). Erro: {Error}",
                    updateResult.ExitCode,
                    string.IsNullOrWhiteSpace(updateResult.StdErr) ? updateResult.StdOut : updateResult.StdErr);
                return false;
            }

        InvalidateCaches();
        _logger.LogInformation("Fontes do Winget reparadas e atualizadas com sucesso.");
        return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("O reparo das fontes do Winget excedeu o tempo limite.");
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("O reparo das fontes do Winget foi cancelado.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao reparar as fontes do Winget.");
            return false;
        }
    }

    /// <summary>
    /// Fixa permanentemente um pacote no Winget utilizando 'winget pin add --id &lt;id&gt; --exact'.
    /// </summary>
    public async Task<bool> PinPackageAsync(string packageId, CancellationToken ct = default)
    {
        ValidatePackageId(packageId);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            var result = await ExecuteWingetWithResultAsync(
                $"pin add --id {packageId} --exact --accept-source-agreements --disable-interactivity",
                timeoutCts.Token).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                InvalidateCaches();
                _logger.LogInformation("Pacote {PackageId} fixado no Winget com sucesso (winget pin add).", packageId);
                return true;
            }

            _logger.LogWarning(
                "Falha ao fixar pacote {PackageId} via 'winget pin add' (ExitCode: {ExitCode}). StdErr: {StdErr}",
                packageId,
                result.ExitCode,
                string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao tentar fixar o pacote {PackageId} via Winget CLI.", packageId);
            return false;
        }
    }

    /// <summary>
    /// Remove o pin permanente de um pacote no Winget utilizando 'winget pin remove --id &lt;id&gt; --exact'.
    /// </summary>
    public async Task<bool> UnpinPackageAsync(string packageId, CancellationToken ct = default)
    {
        ValidatePackageId(packageId);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            var result = await ExecuteWingetWithResultAsync(
                $"pin remove --id {packageId} --exact --accept-source-agreements --disable-interactivity",
                timeoutCts.Token).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                InvalidateCaches();
                _logger.LogInformation("Pin removido do pacote {PackageId} no Winget com sucesso (winget pin remove).", packageId);
                return true;
            }

            _logger.LogDebug(
                "Comando 'winget pin remove' para {PackageId} retornou código {ExitCode}.",
                packageId,
                result.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao tentar remover o pin do pacote {PackageId} via Winget CLI.", packageId);
            return false;
        }
    }

    private void InvalidateCaches()
    {
        _cacheExpiryTime = DateTime.MinValue;
        _installedCacheExpiryTime = DateTime.MinValue;
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteWingetWithResultAsync(string arguments, CancellationToken ct)
    {
        if (_commandRunner != null)
        {
            return await _commandRunner(arguments, ct).ConfigureAwait(false);
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = processStartInfo };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "O executável 'winget' não foi encontrado ao executar '{Arguments}'.", arguments);
            return (-1, string.Empty, "O executável 'winget' não foi encontrado.");
        }

        await using var processKiller = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); }
            catch { }
        }).ConfigureAwait(false);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return (process.ExitCode, stdout, stderr);
    }

    private async Task<bool> ExecuteElevatedRepairSourcesAsync(CancellationToken ct)
    {
        if (_elevatedRepairRunner != null)
        {
            return await _elevatedRepairRunner(ct).ConfigureAwait(false);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"winget source reset --force && winget source update\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            await using var processKiller = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); }
                catch { }
            }).ConfigureAwait(false);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Elevação de administrador recusada ou falhou ao reparar as fontes do Winget.");
            return false;
        }
    }

    internal static bool IsPermissionFailure(int exitCode, string stdOut, string stdErr)
    {
        if (exitCode is 5 or unchecked((int)0x80070005))
        {
            return true;
        }

        var combined = $"{stdOut} {stdErr}";
        return combined.Contains("administrator", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("administrador", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("acesso negado", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("permiss", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("elevation", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("eleva", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("privil", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("0x80070005", StringComparison.OrdinalIgnoreCase);
    }
}
