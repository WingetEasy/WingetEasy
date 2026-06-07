using System;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

public sealed class SchedulerService : ISchedulerService, IDisposable
{
    private readonly ISettingsRepository _settingsRepository;
    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    private const string SettingsKey = "ScheduleConfig";

    public event EventHandler? CheckRequested;

    public SchedulerService(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task StartAsync()
    {
        var config = await _settingsRepository.GetAsync<ScheduleConfig>(SettingsKey).ConfigureAwait(false)
        ?? new ScheduleConfig(ScheduleFrequency.OnceDaily, TimeSpan.Zero);

        StartTimer(config.Frequency);
    }

    public async Task UpdateScheduleAsync(ScheduleConfig config)
    {
        await _settingsRepository.SetAsync(SettingsKey, config).ConfigureAwait(false);

        StopTimer();
        StartTimer(config.Frequency);
    }

    private void StartTimer(ScheduleFrequency frequency)
    {
        if (frequency == ScheduleFrequency.Manual) return;

        var interval = frequency switch
        {
            ScheduleFrequency.TwiceDaily => TimeSpan.FromHours(12),
            ScheduleFrequency.OnceDaily => TimeSpan.FromDays(1),
            ScheduleFrequency.Weekly => TimeSpan.FromDays(7),
            _ => TimeSpan.FromDays(1)
        };

        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(interval);

        _loopTask = RunLoopAsync(_timer, _cts.Token);
    }

    private async Task RunLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                CheckRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // Timer foi cancelado, sair do loop
        }
    }

    private void StopTimer()
    {
        _cts?.Cancel();

        if(_loopTask != null && !_loopTask.IsCompleted)
        {
            try
            {
                _loopTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignorar exceções de cancelamento ou timeout
            }
        }
        _timer?.Dispose();
        _cts?.Dispose();

        _timer = null;
        _cts = null;
        _loopTask = null;
    }

    public void Dispose()
    {
        StopTimer();
    }
}
