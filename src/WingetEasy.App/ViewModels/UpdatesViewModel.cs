using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WingetEasy.Core.Interfaces;

namespace WingetEasy.App.ViewModels;

/// <summary>
/// ViewModel central responsável por gerenciar a busca e aplicação de atualizações.
/// </summary>
public partial class UpdatesViewModel : ObservableObject
{
    private readonly IWingetService _wingetService;
    private readonly IUpdateService _updateService;
    private readonly INotificationService _notificationService;
    private readonly IPackageRepository _packageRepository;
    private readonly ILogger<UpdatesViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _cancellationTokenSource;

    // Sintaxe moderna para propriedades observáveis
    [ObservableProperty]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdatingVisibility))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial bool HasUpdates { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int UpdateProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchStatus))]
    public partial int BatchCompleted { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchStatus))]
    public partial int BatchTotal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSummary))]
    public partial int SuccessfulUpdates { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSummary))]
    public partial int FailedUpdates { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateSummaryVisible { get; set; }

    [ObservableProperty]
    public partial string CurrentUpdatingPackage { get; set; } = string.Empty;

    public ObservableCollection<PackageItemViewModel> Packages { get; } = new();

    public int SelectedCount => Packages.Count(p => p.IsSelected);
    public string SelectedCountText => $"{SelectedCount} selecionados";
    public Visibility IsUpdatingVisibility => IsUpdating ? Visibility.Visible : Visibility.Collapsed;
    public string BatchStatus => $"Atualizando {BatchCompleted} de {BatchTotal}...";
    public string UpdateSummary => $"{SuccessfulUpdates} atualizadas com sucesso; {FailedUpdates} falharam.";

    public UpdatesViewModel(
        IWingetService wingetService,
        IUpdateService updateService,
        INotificationService notificationService,
        IPackageRepository packageRepository,
        ILogger<UpdatesViewModel> logger,
        DispatcherQueue dispatcherQueue)
    {
        _wingetService = wingetService;
        _updateService = updateService;
        _notificationService = notificationService;
        _packageRepository = packageRepository;
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;
    }

    public void UpdateSelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        UpdateSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        try
        {
            IsChecking = true;
            StatusMessage = "Procurando atualizações disponíveis...";
            _logger.LogInformation("Iniciando verificação de atualizações...");

            using var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;

            var updates = await _wingetService
                .CheckUpdatesAsync(cancellationTokenSource.Token)
                .ConfigureAwait(false);

            await RunOnUIAsync(() =>
            {
                Packages.Clear();
                IsUpdateSummaryVisible = false;

                foreach (var package in updates)
                {
                    var category = package.IsSecurityUpdate ? "Segurança" : package.Category.ToString();
                    Packages.Add(new PackageItemViewModel(
                        package.Id,
                        package.Name,
                        package.CurrentVersion,
                        package.AvailableVersion,
                        package.Source ?? string.Empty,
                        category,
                        this));
                }

                HasUpdates = Packages.Any();
                StatusMessage = HasUpdates
                    ? $"{Packages.Count} atualizações encontradas."
                    : "O seu sistema está totalmente atualizado!";

                UpdateSelectionState();

                if (HasUpdates)
                {
                    _notificationService.ShowUpdatesAvailable(Packages.Count);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await RunOnUIAsync(() => StatusMessage = "Verificação cancelada.").ConfigureAwait(false);
            _logger.LogInformation("A verificação de atualizações foi cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            await RunOnUIAsync(() => StatusMessage = "Ocorreu um erro ao verificar as atualizações.").ConfigureAwait(false);
            _logger.LogError(ex, "Erro fatal não tratado em CheckUpdatesAsync.");
        }
        finally
        {
            _cancellationTokenSource = null;
            await RunOnUIAsync(() => IsChecking = false).ConfigureAwait(false);
        }
    }

    private bool CanUpdate() => SelectedCount > 0 && !IsUpdating;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateSelectedAsync()
    {
        try
        {
            IsUpdating = true;
            using var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;

            var pacotesParaAtualizar = Packages.Where(p => p.IsSelected).ToList();
            await RunOnUIAsync(() =>
            {
                BatchCompleted = 0;
                BatchTotal = pacotesParaAtualizar.Count;
                SuccessfulUpdates = 0;
                FailedUpdates = 0;
                IsUpdateSummaryVisible = false;
                UpdateProgress = 0;

                foreach (var package in pacotesParaAtualizar)
                {
                    package.UpdateState = PackageUpdateState.Queued;
                    package.Progress = 0;
                    package.ErrorMessage = string.Empty;
                }
            }).ConfigureAwait(false);

            _logger.LogInformation("Iniciando atualização de {Count} pacotes.", pacotesParaAtualizar.Count);

            var progress = new Progress<(string name, int pct, int cur, int total)>(update =>
            {
                _ = RunOnUIAsync(() =>
                {
                    var package = pacotesParaAtualizar.FirstOrDefault(p => p.Id == update.name);
                    if (package is not null)
                    {
                        package.UpdateState = PackageUpdateState.Updating;
                        package.Progress = update.pct;
                    }

                    BatchCompleted = Math.Max(0, update.cur - 1);
                    CurrentUpdatingPackage = package?.Name ?? update.name;
                    StatusMessage = $"Atualizando {update.cur} de {update.total}...";
                    UpdateProgress = (int)(((update.cur - 1) * 100d + update.pct) / update.total);
                });
            });

            var results = await _updateService
                .ExecuteUpdatesAsync(
                    pacotesParaAtualizar.Select(package => package.Id),
                    progress,
                    cancellationTokenSource.Token)
                .ConfigureAwait(false);

            var failedCount = results.Count(result => !result.Success);
            await RunOnUIAsync(() =>
            {
                foreach (var result in results)
                {
                    var package = pacotesParaAtualizar.FirstOrDefault(p => p.Id == result.PackageId);
                    if (package is null)
                    {
                        continue;
                    }

                    package.UpdateState = result.Success
                        ? PackageUpdateState.Completed
                        : PackageUpdateState.Failed;
                    package.Progress = result.Success ? 100 : package.Progress;
                    package.ErrorMessage = result.ErrorMessage ?? string.Empty;
                }

                BatchCompleted = results.Count;
                SuccessfulUpdates = results.Count(result => result.Success);
                FailedUpdates = failedCount;
                UpdateProgress = 100;
                IsUpdateSummaryVisible = true;
                StatusMessage = failedCount == 0
                    ? $"{SuccessfulUpdates} atualizações concluídas com sucesso."
                    : $"{SuccessfulUpdates} atualizações concluídas; {FailedUpdates} falharam.";
            }).ConfigureAwait(false);
            _logger.LogInformation("Processo de atualização finalizado.");
            _notificationService.ShowUpdateComplete(SuccessfulUpdates, FailedUpdates);

            await RunOnUIAsync(() => HasUpdates = false).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await RunOnUIAsync(() => StatusMessage = "Instalação cancelada em andamento.").ConfigureAwait(false);
            _logger.LogWarning("O processo de atualização foi interrompido.");
        }
        catch (Exception ex)
        {
            await RunOnUIAsync(() => StatusMessage = $"Erro ao atualizar: {CurrentUpdatingPackage}").ConfigureAwait(false);
            _logger.LogError(ex, "Erro ao atualizar o pacote {PackageName}.", CurrentUpdatingPackage);
        }
        finally
        {
            _cancellationTokenSource = null;
            await RunOnUIAsync(() =>
            {
                IsUpdating = false;
                CurrentUpdatingPackage = string.Empty;
                UpdateProgress = 0;
            }).ConfigureAwait(false);
        }
    }

    private Task RunOnUIAsync(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completionSource = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completionSource.SetResult();
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        }))
        {
            completionSource.SetException(
                new InvalidOperationException("Não foi possível agendar a atualização na thread da UI."));
        }

        return completionSource.Task;
    }

    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("CancelCommand acionado via interface.");
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var package in Packages)
        {
            package.IsSelected = true;
        }
        UpdateSelectionState();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var package in Packages)
        {
            package.IsSelected = false;
        }
        UpdateSelectionState();
    }

    public void RemovePackageFromList(PackageItemViewModel package)
    {
        Packages.Remove(package);
        UpdateSelectionState();
    }

    public async Task SkipPackagePermanentlyAsync(PackageItemViewModel package)
    {
        await _packageRepository.SkipPackageAsync(package.Id).ConfigureAwait(true);
        _logger.LogInformation("Pacote {PackageId} adicionado à lista de ignorados permanentes.", package.Id);

        RemovePackageFromList(package);
    }
}
