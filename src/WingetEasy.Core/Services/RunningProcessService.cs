using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WingetEasy.Core.Interfaces;

namespace WingetEasy.Core.Services;

/// <summary>
/// Representa o instantâneo de um processo no sistema para inspeção e controle de encerramento seguro.
/// </summary>
public sealed class ProcessSnapshot
{
    public int Id { get; }
    public string ProcessName { get; }
    public string MainWindowTitle { get; }
    public IntPtr MainWindowHandle { get; }
    public bool HasVisibleWindow { get; }
    public Func<bool> HasExitedFunc { get; }
    public Action? CloseMainWindowAction { get; }
    public Action? KillAction { get; }

    public bool HasExited => HasExitedFunc();

    public ProcessSnapshot(
        int id,
        string processName,
        string mainWindowTitle,
        IntPtr mainWindowHandle,
        bool hasVisibleWindow,
        Func<bool> hasExitedFunc,
        Action? closeMainWindowAction = null,
        Action? killAction = null)
    {
        Id = id;
        ProcessName = processName;
        MainWindowTitle = mainWindowTitle;
        MainWindowHandle = mainWindowHandle;
        HasVisibleWindow = hasVisibleWindow;
        HasExitedFunc = hasExitedFunc;
        CloseMainWindowAction = closeMainWindowAction;
        KillAction = killAction;
    }
}

/// <summary>
/// Implementação do serviço de detecção e gerenciamento de processos ativos de aplicativos WinGet.
/// </summary>
public sealed class RunningProcessService : IRunningProcessService
{
    private readonly Func<IEnumerable<ProcessSnapshot>> _processProvider;
    private readonly ILogger<RunningProcessService> _logger;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// Mapa de aplicativos populares e respectivos nomes de processo no Windows.
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownProcessMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Discord.Discord"] = ["Discord"],
        ["Discord.Canary"] = ["DiscordCanary"],
        ["Discord.PTB"] = ["DiscordPTB"],
        ["Microsoft.Teams"] = ["Teams", "ms-teams", "msteams"],
        ["Microsoft.Teams.Free"] = ["Teams", "ms-teams", "msteams"],
        ["Microsoft.Teams.Classic"] = ["Teams"],
        ["Zoom.Zoom"] = ["Zoom"],
        ["Zoom.Workplace"] = ["Zoom", "Zoom Workplace"],
        ["Google.Chrome"] = ["chrome"],
        ["Google.Chrome.Dev"] = ["chrome"],
        ["Google.Chrome.Beta"] = ["chrome"],
        ["Google.Chrome.Canary"] = ["chrome"],
        ["Microsoft.Edge"] = ["msedge"],
        ["Microsoft.Edge.Dev"] = ["msedge"],
        ["Microsoft.Edge.Beta"] = ["msedge"],
        ["Microsoft.Edge.Canary"] = ["msedge"],
        ["Brave.Brave"] = ["brave"],
        ["BraveSoftware.BraveBrowser"] = ["brave"],
        ["Microsoft.VisualStudioCode"] = ["Code"],
        ["Microsoft.VisualStudioCode.Insiders"] = ["Code - Insiders"],
        ["Obsidian.Obsidian"] = ["Obsidian"],
        ["Spotify.Spotify"] = ["Spotify"],
        ["Valve.Steam"] = ["steam"],
        ["Notepad++.Notepad++"] = ["notepad++"],
        ["VideoLAN.VLC"] = ["vlc"],
        ["SlackTechnologies.Slack"] = ["slack"],
        ["Telegram.TelegramDesktop"] = ["Telegram"],
        ["WhatsApp.WhatsApp"] = ["WhatsApp"],
        ["Mozilla.Firefox"] = ["firefox"],
        ["Mozilla.Firefox.DeveloperEdition"] = ["firefox"],
        ["Mozilla.Thunderbird"] = ["thunderbird"],
        ["Opera.Opera"] = ["opera"],
        ["Opera.OperaGX"] = ["opera"],
        ["GitHub.GitHubDesktop"] = ["GitHubDesktop"],
        ["Postman.Postman"] = ["Postman"],
        ["Figma.Figma"] = ["Figma"],
        ["Docker.DockerDesktop"] = ["Docker Desktop", "com.docker.backend"],
        ["JetBrains.IntelliJIDEA.Community"] = ["idea64"],
        ["JetBrains.IntelliJIDEA.Ultimate"] = ["idea64"],
        ["JetBrains.PyCharm.Community"] = ["pycharm64"],
        ["JetBrains.PyCharm.Professional"] = ["pycharm64"],
        ["JetBrains.Rider"] = ["rider64"],
        ["JetBrains.WebStorm"] = ["webstorm64"],
        ["Microsoft.PowerToys"] = ["PowerToys"],
        ["Microsoft.WindowsTerminal"] = ["wt", "WindowsTerminal"]
    };

    public RunningProcessService(ILogger<RunningProcessService>? logger = null)
        : this(GetSystemProcessSnapshots, logger)
    {
    }

    public RunningProcessService(Func<IEnumerable<ProcessSnapshot>> processProvider, ILogger<RunningProcessService>? logger = null)
    {
        _processProvider = processProvider ?? GetSystemProcessSnapshots;
        _logger = logger ?? NullLogger<RunningProcessService>.Instance;
    }

    public bool IsAppRunning(string packageId, string? packageName = null)
    {
        return FindMatchingSnapshots(packageId, packageName).Any(p => !p.HasExited);
    }

    public bool IsAppRunningWithVisibleWindow(string packageId, string? packageName = null)
    {
        return FindMatchingSnapshots(packageId, packageName).Any(p => !p.HasExited && p.HasVisibleWindow);
    }

    public IReadOnlyList<string> GetRunningProcessNames(string packageId, string? packageName = null)
    {
        return FindMatchingSnapshots(packageId, packageName)
            .Where(p => !p.HasExited)
            .Select(p => p.ProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> CloseAppProcessesAsync(
        string packageId,
        string? packageName = null,
        bool killIfUnresponsive = true,
        TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(4);
        var matching = FindMatchingSnapshots(packageId, packageName).Where(p => !p.HasExited).ToList();

        if (matching.Count == 0)
        {
            return true;
        }

        _logger.LogInformation("Solicitando fechamento gracioso de {Count} processo(s) para {PackageId}...", matching.Count, packageId);

        // 1. Tenta fechar janela principal graciosamente
        foreach (var p in matching)
        {
            try
            {
                p.CloseMainWindowAction?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao tentar fechar a janela principal do processo {ProcessName}.", p.ProcessName);
            }
        }

        // Aguarda encerramento dentro do limite de tempo
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < effectiveTimeout)
        {
            if (matching.All(p => p.HasExited))
            {
                _logger.LogInformation("Processos de {PackageId} fechados com sucesso de forma graciosa.", packageId);
                return true;
            }
            await Task.Delay(200).ConfigureAwait(false);
        }

        // 2. Se ainda restarem processos ativos e killIfUnresponsive for verdadeiro
        if (killIfUnresponsive)
        {
            var remaining = matching.Where(p => !p.HasExited).ToList();
            if (remaining.Count > 0)
            {
                _logger.LogWarning("Tempo limite esgotado. Forçando encerramento de {Count} processo(s) de {PackageId}...", remaining.Count, packageId);
                foreach (var p in remaining)
                {
                    try
                    {
                        p.KillAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Erro ao forçar encerramento do processo {ProcessName}.", p.ProcessName);
                    }
                }

                var killSw = Stopwatch.StartNew();
                while (killSw.Elapsed < TimeSpan.FromSeconds(2))
                {
                    if (matching.All(p => p.HasExited))
                    {
                        return true;
                    }
                    await Task.Delay(150).ConfigureAwait(false);
                }
            }
        }

        return matching.All(p => p.HasExited);
    }

    private IEnumerable<ProcessSnapshot> FindMatchingSnapshots(string packageId, string? packageName)
    {
        var snapshots = _processProvider();
        var candidateNames = GetCandidateProcessNames(packageId, packageName);

        return snapshots.Where(snapshot =>
        {
            if (candidateNames.Contains(snapshot.ProcessName))
            {
                return true;
            }

            // Heurística de correspondência aproximada caso não esteja mapeado
            foreach (var candidate in candidateNames)
            {
                if (string.Equals(snapshot.ProcessName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        });
    }

    internal static HashSet<string> GetCandidateProcessNames(string packageId, string? packageName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(packageId))
        {
            return set;
        }

        // 1. Mapeamento direto conhecido
        if (KnownProcessMap.TryGetValue(packageId, out var knownProcesses))
        {
            foreach (var kp in knownProcesses)
            {
                set.Add(kp);
            }
        }

        // 2. Fragmento final do ID (Publisher.Product -> Product)
        var lastDotIndex = packageId.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < packageId.Length - 1)
        {
            var productPart = packageId[(lastDotIndex + 1)..];
            set.Add(productPart);
        }
        else
        {
            set.Add(packageId);
        }

        // 3. Nome do pacote limpo de espaços e pontuações
        if (!string.IsNullOrWhiteSpace(packageName))
        {
            var cleaned = new string(packageName.Where(char.IsLetterOrDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                set.Add(cleaned);
            }

            // Ex: "Google Chrome" -> "chrome"
            var words = packageName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                set.Add(words[^1]);
            }
        }

        return set;
    }

    private static IEnumerable<ProcessSnapshot> GetSystemProcessSnapshots()
    {
        var list = new List<ProcessSnapshot>();
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return list;
        }

        foreach (var proc in processes)
        {
            try
            {
                var id = proc.Id;
                var name = proc.ProcessName;
                var handle = IntPtr.Zero;
                var title = string.Empty;
                bool isVisible = false;

                try
                {
                    handle = proc.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        title = proc.MainWindowTitle;
                        isVisible = OperatingSystem.IsWindows()
                            ? IsWindowVisible(handle) && !string.IsNullOrWhiteSpace(title)
                            : !string.IsNullOrWhiteSpace(title);
                    }
                }
                catch
                {
                    // Alguns processos do sistema bloqueiam leitura de MainWindowHandle
                }

                list.Add(new ProcessSnapshot(
                    id,
                    name,
                    title,
                    handle,
                    isVisible,
                    () => { try { return proc.HasExited; } catch { return true; } },
                    () =>
                    {
                        try
                        {
                            if (!proc.HasExited && proc.MainWindowHandle != IntPtr.Zero)
                            {
                                proc.CloseMainWindow();
                            }
                        }
                        catch { }
                    },
                    () =>
                    {
                        try
                        {
                            if (!proc.HasExited)
                            {
                                proc.Kill(true);
                            }
                        }
                        catch { }
                    }
                ));
            }
            catch
            {
                // Processo pode encerrar no meio da iteração
            }
        }

        return list;
    }
}

