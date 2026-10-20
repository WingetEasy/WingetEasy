using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

/// <summary>
/// Integra o WingetEasy ao Agendador de Tarefas do Windows usando schtasks.exe.
/// </summary>
public sealed class WindowsTaskSchedulerService : ITaskSchedulerService
{
    public const string TaskName = "WingetEasy\\AutoCheck";
    private const string SchtasksExecutable = "schtasks.exe";

    private readonly ITaskSchedulerProcessRunner _processRunner;
    private readonly ILogger<WindowsTaskSchedulerService> _logger;
    private readonly string _applicationPath;

    public WindowsTaskSchedulerService(
        ITaskSchedulerProcessRunner processRunner,
        ILogger<WindowsTaskSchedulerService> logger,
        string? applicationPath = null)
    {
        _processRunner = processRunner;
        _logger = logger;
        _applicationPath = applicationPath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível determinar o caminho do aplicativo.");
    }

    public async Task CreateOrUpdateTaskAsync(ScheduleConfig config, CancellationToken ct = default)
    {
        if (config.Frequency == ScheduleFrequency.Manual)
        {
            await DeleteTaskAsync(ct).ConfigureAwait(false);
            return;
        }

        var arguments = BuildCreateArguments(config, _applicationPath);
        var exitCode = await _processRunner.RunAsync(SchtasksExecutable, arguments, ct).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"schtasks.exe falhou ao registrar a tarefa. Código: {exitCode}.");
        }

        _logger.LogInformation("Tarefa nativa {TaskName} registrada para {Frequency}.", TaskName, config.Frequency);
    }

    public async Task<bool> TaskExistsAsync(CancellationToken ct = default)
    {
        var exitCode = await _processRunner.RunAsync(
            SchtasksExecutable,
            $"/Query /TN {Quote(TaskName)}",
            ct).ConfigureAwait(false);
        return exitCode == 0;
    }

    public async Task DeleteTaskAsync(CancellationToken ct = default)
    {
        var exitCode = await _processRunner.RunAsync(
            SchtasksExecutable,
            $"/Delete /TN {Quote(TaskName)} /F",
            ct).ConfigureAwait(false);
        if (exitCode is not 0 and not 1)
        {
            throw new InvalidOperationException($"schtasks.exe falhou ao remover a tarefa. Código: {exitCode}.");
        }

        _logger.LogInformation("Tarefa nativa {TaskName} removida ou já inexistente.", TaskName);
    }

    internal static string BuildCreateArguments(ScheduleConfig config, string applicationPath)
    {
        var scheduleArguments = config.Frequency switch
        {
            ScheduleFrequency.TwiceDaily => "/SC HOURLY /MO 12",
            ScheduleFrequency.OnceDaily => $"/SC DAILY /ST {FormatTime(config.CheckTime)}",
            ScheduleFrequency.Weekly => $"/SC WEEKLY /D {FormatWeekDay(config)} /ST {FormatTime(config.CheckTime)}",
            _ => throw new ArgumentOutOfRangeException(nameof(config), "A frequência Manual não cria tarefa.")
        };

        var taskAction = $"{Quote(applicationPath)} --check";
        return $"/Create /TN {Quote(TaskName)} /TR {Quote(taskAction)} {scheduleArguments} /F";
    }

    private static string FormatTime(TimeSpan time) =>
        (time == TimeSpan.Zero ? DateTime.Now.TimeOfDay : time).ToString(@"hh\:mm");

    private static string FormatWeekDay(ScheduleConfig config)
    {
        var day = config.WeekDays is { Length: > 0 } ? config.WeekDays[0] : 0;
        return day switch
        {
            0 => "SUN",
            1 => "MON",
            2 => "TUE",
            3 => "WED",
            4 => "THU",
            5 => "FRI",
            6 => "SAT",
            _ => throw new ArgumentOutOfRangeException(nameof(config), "Dia da semana inválido.")
        };
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}

/// <summary>
/// Implementação real da execução de processos do Windows.
/// </summary>
public sealed class WindowsTaskSchedulerProcessRunner : ITaskSchedulerProcessRunner
{
    public async Task<int> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Não foi possível iniciar {fileName}.");
        }
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }
}
