using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using WingetEasy.Core.Interfaces;

namespace WingetEasy.App.ViewModels;

/// <summary>
/// ViewModel central responsável por gerenciar a busca e aplicação de atualizações.
/// </summary>
public partial class UpdatesViewModel : ObservableObject
{
    private readonly IWingetService _wingetService;
    private readonly IUpdateService _updateService;
    private readonly IPackageRepository _packageRepository;
    private readonly ILogger<UpdatesViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _cancellationTokenSource;

    // Sintaxe moderna para propriedades observáveis
    [ObservableProperty]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial bool HasUpdates { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int UpdateProgress { get; set; }

    [ObservableProperty]
    public partial string CurrentUpdatingPackage { get; set; } = string.Empty;

    public ObservableCollection<PackageItemViewModel> Packages { get; } = new();

    public int SelectedCount => Packages.Count(p => p.IsSelected);

    public UpdatesViewModel(
        IWingetService wingetService,
        IUpdateService updateService,
        IPackageRepository packageRepository,
        ILogger<UpdatesViewModel> logger,
        DispatcherQueue dispatcherQueue)
    {
        _wingetService = wingetService;
        _updateService = updateService;
        _packageRepository = packageRepository;
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;
    }

    public void UpdateSelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
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
            _logger.LogInformation("Iniciando atualização de {Count} pacotes.", pacotesParaAtualizar.Count);

            var progress = new Progress<(string name, int pct, int cur, int total)>(update =>
            {
                _ = RunOnUIAsync(() =>
                {
                    CurrentUpdatingPackage = update.name;
                    StatusMessage = $"A atualizar {update.name} ({update.cur} de {update.total})...";
                    UpdateProgress = update.pct;
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
                StatusMessage = failedCount == 0
                    ? "Atualizações concluídas com sucesso!"
                    : $"{failedCount} atualizações falharam.";
            }).ConfigureAwait(false);
            _logger.LogInformation("Processo de atualização finalizado.");

            await RunOnUIAsync(() =>
            {
                Packages.Clear();
                HasUpdates = false;
                UpdateSelectionState();
            }).ConfigureAwait(false);
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
