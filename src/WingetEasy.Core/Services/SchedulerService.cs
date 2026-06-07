using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

public sealed class SchedulerService : ISchedulerService, IDisposable
{
    private readonly ISettingsRepository _settingsRepository;

    // Dependências de notificação, log e a nossa nova classe interna de Circuit Breaker
    private readonly INotificationService _notificationService;
    private readonly ILogger<SchedulerService> _logger;
    private readonly CircuitBreaker _circuitBreaker;


    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    private const string SettingsKey = "ScheduleConfig";

    public event EventHandler? CheckRequested;

    public SchedulerService(
        ISettingsRepository settingsRepository,
        INotificationService notificationService,
        ILogger<SchedulerService> logger)
    {
        _settingsRepository = settingsRepository;
        _notificationService = notificationService;
        _logger = logger;
        _circuitBreaker = new CircuitBreaker();
    }

    public async Task StartAsync()
    {
        var config = await _settingsRepository.GetAsync<ScheduleConfig>(SettingsKey).ConfigureAwait(false)
        ?? new ScheduleConfig(ScheduleFrequency.OnceDaily, TimeSpan.Zero);

        StartTimer(config.Frequency);

        // Log indicando que o agendador arrancou
        _logger.LogInformation("SchedulerService iniciado com a frequência: {Frequency}", config.Frequency);
    }

    public async Task UpdateScheduleAsync(ScheduleConfig config)
    {
        await _settingsRepository.SetAsync(SettingsKey, config).ConfigureAwait(false);

        StopTimer();
        StartTimer(config.Frequency);

        //Log de mudança de configuração
        _logger.LogInformation("Schedule atualizado para: {Frequency}", config.Frequency);
    }

    // método para registrar SUCESSO (vem da Interface)
    public void RecordSuccess()
    {
        if (_circuitBreaker.IsOpen)
        {
            _logger.LogInformation("Circuit Breaker FECHADO. Verificação bem-sucedida recebida, verificações automáticas retomadas.");
        }

        _circuitBreaker.RecordSuccess();
    }

    // método para registrar FALHA (vem da Interface)
    public void RecordFailure()
    {
        bool wasOpen = _circuitBreaker.IsOpen;

        _circuitBreaker.RecordFailure();

        // se acabou de abrir agora atingiu 3 falhas
        if (!wasOpen && _circuitBreaker.IsOpen)
        {
            _logger.LogWarning("Circuit Breaker ABERTO! 3 falhas consecutivas. Verificações automáticas pausadas por 24h.");
            _notificationService.ShowError("Verificações Pausadas", "Ocorreram falhas repetidas ao buscar atualizações. As verificações automáticas foram pausadas por 24h.");
        }
        else if (!_circuitBreaker.IsOpen)
        {
            _logger.LogDebug("Falha registrada no agendador. Falha atual: {Count}/3", _circuitBreaker.Failures);
        }
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
                // Interrompe o disparo invisível se houver falha contínua
                if (_circuitBreaker.IsOpen)
                {
                    _logger.LogInformation("Disparo de verificação suprimido: o Circuit Breaker está aberto.");
                    continue; // Pula o disparo e vai para o próximo ciclo de tempo
                }

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

    // A classe interna que controla o estado do limite de falhas
    private sealed class CircuitBreaker
    {
        private int _failures = 0;
        private const int Threshold = 3;
        private DateTime? _openedAt;

        public int Failures => _failures;

        // Fica aberto (bloqueando) se tiver data de abertura E não tiver passado 24h
        public bool IsOpen => _openedAt.HasValue && DateTime.UtcNow - _openedAt.Value < TimeSpan.FromHours(24);

        public void RecordSuccess()
        {
            _failures = 0;
            _openedAt = null;
        }

        public void RecordFailure()
        {
            if (++_failures >= Threshold)
            {
                // Só atualiza a data na 3ª falha. Se falhar uma 4ª vez enquanto aberto, mantém a data original.
                _openedAt ??= DateTime.UtcNow;
            }
        }
    }
}
