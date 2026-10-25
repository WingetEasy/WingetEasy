using WingetEasy.App.Services;
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
using WingetEasy.Core.Models;

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
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<UpdatesViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ObservableCollection<PackageItemViewModel> _allPackages = new();
    private string _searchText = string.Empty;
    private string _categoryFilter = "Todos";
    private string _sortBy = "Nome";
    private DateTime? _lastCheckTime;

    public bool HasCheckedOnce { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(IsProgressBarVisible))]
    [NotifyPropertyChangedFor(nameof(HeroTitle))]
    [NotifyPropertyChangedFor(nameof(HeroSubtitle))]
    [NotifyPropertyChangedFor(nameof(HeaderSubtitle))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateAllCommand))]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdatingVisibility))]
    [NotifyPropertyChangedFor(nameof(FlyoutNotUpdatingVisibility))]
    [NotifyPropertyChangedFor(nameof(FlyoutStatusText))]
    [NotifyPropertyChangedFor(nameof(FlyoutUpdatingButtonText))]
    [NotifyPropertyChangedFor(nameof(CurrentUpdatingPackageDisplay))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(IsProgressBarVisible))]
    [NotifyPropertyChangedFor(nameof(HeroTitle))]
    [NotifyPropertyChangedFor(nameof(HeroSubtitle))]
    [NotifyPropertyChangedFor(nameof(HeaderSubtitle))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateAllCommand))]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroTitle))]
    [NotifyPropertyChangedFor(nameof(HeroSubtitle))]
    [NotifyPropertyChangedFor(nameof(HeaderSubtitle))]
    [NotifyPropertyChangedFor(nameof(UpdateAllButtonText))]
    [NotifyPropertyChangedFor(nameof(FlyoutStatusText))]
    [NotifyPropertyChangedFor(nameof(FlyoutHasUpdatesVisibility))]
    [NotifyPropertyChangedFor(nameof(FlyoutCleanVisibility))]
    [NotifyPropertyChangedFor(nameof(FlyoutUpdateButtonText))]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    [NotifyPropertyChangedFor(nameof(AvailableCountBadgeVisibility))]
    [NotifyCanExecuteChangedFor(nameof(UpdateAllCommand))]
    public partial bool HasUpdates { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Clique em Verificar agora para buscar atualizações.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateProgressPercentText))]
    public partial int UpdateProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchStatus))]
    [NotifyPropertyChangedFor(nameof(HeroSubtitle))]
    [NotifyPropertyChangedFor(nameof(BatchStatusDisplay))]
    [NotifyPropertyChangedFor(nameof(FlyoutStatusText))]
    [NotifyPropertyChangedFor(nameof(FlyoutUpdatingButtonText))]
    public partial int BatchCompleted { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchStatus))]
    [NotifyPropertyChangedFor(nameof(HeroSubtitle))]
    [NotifyPropertyChangedFor(nameof(BatchStatusDisplay))]
    [NotifyPropertyChangedFor(nameof(FlyoutStatusText))]
    [NotifyPropertyChangedFor(nameof(FlyoutUpdatingButtonText))]
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
    [NotifyPropertyChangedFor(nameof(CurrentUpdatingPackageDisplay))]
    [NotifyPropertyChangedFor(nameof(FlyoutStatusText))]
    [NotifyPropertyChangedFor(nameof(FlyoutUpdatingButtonText))]
    public partial string CurrentUpdatingPackage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AutoUpdateEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string NextCheckScheduleText { get; set; } = "Hoje, 18:00";

    public ObservableCollection<PackageItemViewModel> Packages { get; } = new();
    public ObservableCollection<PackageItemViewModel> DeferredPackages { get; } = new();
    public ObservableCollection<PackageItemViewModel> FlyoutPackages { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string CategoryFilter
    {
        get => _categoryFilter;
        set
        {
            if (SetProperty(ref _categoryFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SortBy
    {
        get => _sortBy;
        set
        {
            if (SetProperty(ref _sortBy, value))
            {
                ApplyFilters();
            }
        }
    }

    public int AvailableCount => _allPackages.Count;
    public Visibility AvailableCountBadgeVisibility => AvailableCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    public int FilteredCount => Packages.Count;
    public int DeferredCount => DeferredPackages.Count;
    public Visibility HasDeferredPackagesVisibility => DeferredPackages.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => (!HasUpdates && !IsChecking) ? Visibility.Visible : Visibility.Collapsed;

    public bool IsBusy => IsChecking || IsUpdating;
    public bool IsNotBusy => !IsChecking && !IsUpdating;
    public Visibility IsProgressBarVisible => (IsChecking || IsUpdating) ? Visibility.Visible : Visibility.Collapsed;

    public string HeroTitle => IsChecking
        ? LocalizationService.Instance.Get("Verificando atualizações…")
        : IsUpdating
            ? LocalizationService.Instance.Get("Atualizando pacotes…")
            : HasUpdates
                ? (LocalizationService.Instance.IsPortuguese
                    ? $"{AvailableCount} {(AvailableCount == 1 ? "atualização disponível" : "atualizações disponíveis")}"
                    : $"{AvailableCount} {(AvailableCount == 1 ? "update available" : "updates available")}")
                : LocalizationService.Instance.Get("Sistema atualizado");

    public string HeroSubtitle => IsChecking
        ? LocalizationService.Instance.Get("Consultando as fontes do Winget.")
        : IsUpdating
            ? (LocalizationService.Instance.IsPortuguese
                ? $"{BatchCompleted} de {BatchTotal} concluídos"
                : $"{BatchCompleted} of {BatchTotal} completed")
            : HasUpdates
                ? LocalizationService.Instance.Get("Recomendamos atualizar para manter tudo seguro e rápido.")
                : LocalizationService.Instance.Get("Seu sistema está em dia. Última verificação agora há pouco.");

    public string HeaderSubtitle => IsChecking
        ? LocalizationService.Instance.Get("Verificando atualizações…")
        : IsUpdating
            ? (LocalizationService.Instance.IsPortuguese
                ? $"Atualizando ({BatchCompleted}/{BatchTotal})…"
                : $"Updating ({BatchCompleted}/{BatchTotal})…")
            : HasUpdates
                ? (LocalizationService.Instance.IsPortuguese
                    ? $"{AvailableCount} {(AvailableCount == 1 ? "atualização disponível" : "atualizações disponíveis")}"
                    : $"{AvailableCount} {(AvailableCount == 1 ? "update available" : "updates available")}")
                : LocalizationService.Instance.Get("Tudo atualizado");

    public string UpdateAllButtonText => HasUpdates
        ? LocalizationService.Instance.Get("Atualizar tudo")
        : LocalizationService.Instance.Get("Tudo atualizado");

    public int SelectedCount => _allPackages.Count(p => p.IsSelected);
    public string SelectedCountText => LocalizationService.Instance.IsPortuguese
        ? $"Total selecionado: {SelectedCount} {(SelectedCount == 1 ? "pacote" : "pacotes")}"
        : $"Total selected: {SelectedCount} {(SelectedCount == 1 ? "package" : "packages")}";
    public string UpdateSelectedButtonText => LocalizationService.Instance.IsPortuguese
        ? $"Atualizar selecionados ({SelectedCount})"
        : $"Update selected ({SelectedCount})";

    public bool IsAllSelected
    {
        get => _allPackages.Count > 0 && _allPackages.All(p => p.IsSelected);
        set
        {
            foreach (var p in _allPackages)
            {
                p.IsSelected = value;
            }
            UpdateSelectionState();
        }
    }

    // Propriedades para o Flyout da Bandeja
    public string FlyoutStatusText
    {
        get
        {
            if (IsUpdating)
            {
                return BatchTotal > 0
                    ? (LocalizationService.Instance.IsPortuguese
                        ? $"Atualizando ({Math.Min(BatchCompleted + 1, BatchTotal)} de {BatchTotal})..."
                        : $"Updating ({Math.Min(BatchCompleted + 1, BatchTotal)} of {BatchTotal})...")
                    : LocalizationService.Instance.Get("Atualizando...");
            }
            return HasUpdates
                ? (LocalizationService.Instance.IsPortuguese
                    ? $"{AvailableCount} {(AvailableCount == 1 ? "atualização disponível" : "atualizações disponíveis")}"
                    : $"{AvailableCount} {(AvailableCount == 1 ? "update available" : "updates available")}")
                : LocalizationService.Instance.Get("Tudo atualizado");
        }
    }

    public Visibility FlyoutNotUpdatingVisibility => IsUpdating ? Visibility.Collapsed : Visibility.Visible;
    public string FlyoutUpdatingButtonText => BatchTotal > 0
        ? (LocalizationService.Instance.IsPortuguese
            ? $"Atualizando ({Math.Min(BatchCompleted + 1, BatchTotal)}/{BatchTotal})..."
            : $"Updating ({Math.Min(BatchCompleted + 1, BatchTotal)}/{BatchTotal})...")
        : LocalizationService.Instance.Get("Atualizando...");
    public string CurrentUpdatingPackageDisplay => !string.IsNullOrWhiteSpace(CurrentUpdatingPackage)
        ? (LocalizationService.Instance.IsPortuguese ? $"Atualizando {CurrentUpdatingPackage}..." : $"Updating {CurrentUpdatingPackage}...")
        : (LocalizationService.Instance.IsPortuguese ? "Preparando atualização..." : "Preparing update...");
    public string BatchStatusDisplay => BatchTotal > 0
        ? (LocalizationService.Instance.IsPortuguese
            ? $"{Math.Min(BatchCompleted + 1, BatchTotal)} de {BatchTotal}"
            : $"{Math.Min(BatchCompleted + 1, BatchTotal)} of {BatchTotal}")
        : string.Empty;
    public string UpdateProgressPercentText => UpdateProgress > 0 ? $"{UpdateProgress}%" : string.Empty;

    public Visibility FlyoutHasUpdatesVisibility => HasUpdates ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FlyoutCleanVisibility => HasUpdates ? Visibility.Collapsed : Visibility.Visible;
    public string FlyoutUpdateButtonText => !HasUpdates
        ? LocalizationService.Instance.Get("Tudo atualizado")
        : IsAllSelected
            ? LocalizationService.Instance.Get("Atualizar tudo")
            : SelectedCount == 0
                ? (LocalizationService.Instance.IsPortuguese ? "Nenhum selecionado" : "None selected")
                : (LocalizationService.Instance.IsPortuguese ? $"Atualizar selecionados ({SelectedCount})" : $"Update selected ({SelectedCount})");
    public Visibility FlyoutMoreVisibility => AvailableCount > 4 ? Visibility.Visible : Visibility.Collapsed;
    public string FlyoutMoreText => AvailableCount > 4
        ? $"+ {AvailableCount - 4} {(AvailableCount - 4 == 1 ? "outro pacote" : "outros pacotes")}"
        : string.Empty;

    public string LastCheckText
    {
        get
        {
            if (!_lastCheckTime.HasValue)
                return "Última verificação há 1 min";

            var mins = Math.Max(1, (int)Math.Round((DateTime.Now - _lastCheckTime.Value).TotalMinutes));
            return $"Última verificação há {mins} min";
        }
    }

    /// <summary>
    /// Callback usado pela view ativa para confirmar a abertura do prompt UAC.
    /// </summary>
    public Func<Task<bool>>? ElevationConfirmation { get; set; }

    /// <summary>
    /// Callback usado pela view ativa para exibir diálogo amigável quando um aplicativo está aberto.
    /// </summary>
    public Func<string, Task<AppRunningResolution>>? AppRunningPrompt { get; set; }

    public Visibility IsUpdatingVisibility => IsUpdating ? Visibility.Visible : Visibility.Collapsed;
    public string BatchStatus => $"Atualizando {BatchCompleted} de {BatchTotal}...";
    public string UpdateSummary => $"{SuccessfulUpdates} atualizadas com sucesso; {FailedUpdates} falharam.";

    public UpdatesViewModel(
        IWingetService wingetService,
        IUpdateService updateService,
        INotificationService notificationService,
        IPackageRepository packageRepository,
        ISettingsRepository settingsRepository,
        ILogger<UpdatesViewModel> logger,
        DispatcherQueue dispatcherQueue)
    {
        _wingetService = wingetService;
        _updateService = updateService;
        _notificationService = notificationService;
        _packageRepository = packageRepository;
        _settingsRepository = settingsRepository;
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;
        _ = InitializeSettingsAsync();
        LocalizationService.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(HeroTitle));
            OnPropertyChanged(nameof(HeroSubtitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(UpdateAllButtonText));
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(UpdateSelectedButtonText));
            OnPropertyChanged(nameof(FlyoutStatusText));
            OnPropertyChanged(nameof(FlyoutUpdatingButtonText));
            OnPropertyChanged(nameof(CurrentUpdatingPackageDisplay));
            OnPropertyChanged(nameof(BatchStatusDisplay));
            OnPropertyChanged(nameof(FlyoutUpdateButtonText));
            if (!HasUpdates && !IsChecking && !IsUpdating)
            {
                StatusMessage = LocalizationService.Instance.Get("Todos os pacotes estão atualizados.");
            }
        };
    }

    partial void OnAutoUpdateEnabledChanged(bool value)
    {
        _ = _settingsRepository.SetAsync("AutoUpdate", value.ToString());
    }

    private async Task InitializeSettingsAsync()
    {
        try
        {
            var raw = await _settingsRepository.GetAsync("AutoUpdate").ConfigureAwait(false);
            var saved = bool.TryParse(raw, out var parsed) && parsed;
            await RunOnUIAsync(() => AutoUpdateEnabled = saved).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível carregar configuração de AutoUpdate.");
        }
    }

    public void UpdateSelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(UpdateSelectedButtonText));
        OnPropertyChanged(nameof(FlyoutUpdateButtonText));
        OnPropertyChanged(nameof(IsAllSelected));
        UpdateSelectedCommand.NotifyCanExecuteChanged();
        UpdateAllCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCountsChanged()
    {
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(AvailableCountBadgeVisibility));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(DeferredCount));
        OnPropertyChanged(nameof(HasDeferredPackagesVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(HeroTitle));
        OnPropertyChanged(nameof(HeroSubtitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(UpdateAllButtonText));
        OnPropertyChanged(nameof(FlyoutStatusText));
        OnPropertyChanged(nameof(FlyoutHasUpdatesVisibility));
        OnPropertyChanged(nameof(FlyoutCleanVisibility));
        OnPropertyChanged(nameof(FlyoutUpdateButtonText));
        OnPropertyChanged(nameof(FlyoutMoreVisibility));
        OnPropertyChanged(nameof(FlyoutMoreText));
        OnPropertyChanged(nameof(LastCheckText));
        UpdateSelectionState();
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (IsChecking)
            return;

        try
        {
            HasCheckedOnce = true;
            await RunOnUIAsync(() =>
            {
                IsChecking = true;
                StatusMessage = "Procurando atualizações disponíveis...";
            }).ConfigureAwait(false);

            _logger.LogInformation("Iniciando verificação de atualizações...");

            using var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;

            var updates = await _wingetService
                .CheckUpdatesAsync(cancellationTokenSource.Token)
                .ConfigureAwait(false);

            _lastCheckTime = DateTime.Now;

            await RunOnUIAsync(() =>
            {
                _allPackages.Clear();
                IsUpdateSummaryVisible = false;

                foreach (var package in updates)
                {
                    // Se o pacote já estava em DeferredPackages, não duplica
                    if (DeferredPackages.Any(d => string.Equals(d.Id, package.Id, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _allPackages.Add(new PackageItemViewModel(
                        package.Id,
                        package.Name,
                        package.CurrentVersion,
                        package.AvailableVersion,
                        package.Source ?? string.Empty,
                        PackageItemViewModel.Classify(package),
                        this));
                }

                ApplyFilters();
                HasUpdates = _allPackages.Any();
                StatusMessage = HasUpdates
                    ? $"{_allPackages.Count} atualizações encontradas."
                    : "O seu sistema está totalmente atualizado!";

                NotifyCountsChanged();
                (Application.Current as App)?.UpdateTrayState(_allPackages.Count);

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
            await RunOnUIAsync(() =>
            {
                IsChecking = false;
                NotifyCountsChanged();
            }).ConfigureAwait(false);
        }
    }

    private bool CanUpdate() => SelectedCount > 0 && !IsUpdating && !IsChecking;
    private bool CanUpdateAll() => _allPackages.Count > 0 && !IsUpdating && !IsChecking;

    [RelayCommand(CanExecute = nameof(CanUpdateAll))]
    private async Task UpdateAllAsync()
    {
        SelectAll();
        await UpdateSelectedAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateSelectedAsync()
    {
        var pacotesParaAtualizar = _allPackages.Where(p => p.IsSelected).ToList();
        await ExecutePackageBatchAsync(pacotesParaAtualizar).ConfigureAwait(false);
    }

    public async Task UpdateSinglePackageAsync(PackageItemViewModel package)
    {
        if (IsUpdating)
            return;

        await ExecutePackageBatchAsync([package]).ConfigureAwait(false);
    }

    private async Task ExecutePackageBatchAsync(System.Collections.Generic.List<PackageItemViewModel> pacotesParaAtualizar)
    {
        if (pacotesParaAtualizar.Count == 0)
            return;

        try
        {
            await RunOnUIAsync(() => IsUpdating = true).ConfigureAwait(false);
            using var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;

            await RunOnUIAsync(() =>
            {
                BatchCompleted = 0;
                BatchTotal = pacotesParaAtualizar.Count;
                SuccessfulUpdates = 0;
                FailedUpdates = 0;
                IsUpdateSummaryVisible = false;
                UpdateProgress = 0;
                CurrentUpdatingPackage = pacotesParaAtualizar.Count > 0 ? pacotesParaAtualizar[0].Name : string.Empty;

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
                        if (update.pct >= 100)
                        {
                            package.UpdateState = PackageUpdateState.Completed;
                            package.Progress = 100;
                            _allPackages.Remove(package);
                            Packages.Remove(package);
                        }
                        else if (update.pct < 0)
                        {
                            package.UpdateState = PackageUpdateState.Failed;
                        }
                        else
                        {
                            package.UpdateState = PackageUpdateState.Updating;
                            package.Progress = update.pct;
                        }
                    }

                    BatchCompleted = update.pct >= 100 ? update.cur : Math.Max(0, update.cur - 1);
                    CurrentUpdatingPackage = package?.Name ?? update.name;
                    StatusMessage = $"Atualizando {update.cur} de {update.total}...";
                    UpdateProgress = (int)(((update.cur - 1) * 100d + Math.Max(0, update.pct)) / update.total);
                    NotifyCountsChanged();
                });
            });

            var results = await _updateService
                .ExecuteUpdatesAsync(
                    pacotesParaAtualizar.Select(package => package.Id),
                    progress,
                    ElevationConfirmation,
                    AppRunningPrompt,
                    isAutoUpdate: false,
                    cancellationTokenSource.Token)
                .ConfigureAwait(false);

            var failedCount = results.Count(result => !result.Success && !result.Skipped);
            var skippedCount = results.Count(result => result.Skipped);
            await RunOnUIAsync(() =>
            {
                foreach (var result in results)
                {
                    var package = pacotesParaAtualizar.FirstOrDefault(p => p.Id == result.PackageId);
                    if (package is null)
                    {
                        continue;
                    }

                    if (result.Skipped)
                    {
                        package.UpdateState = PackageUpdateState.Pending;
                        package.IsSelected = false;
                        package.ErrorMessage = result.ErrorMessage ?? "Atualização adiada (aplicativo em uso).";
                    }
                    else
                    {
                        package.UpdateState = result.Success
                            ? PackageUpdateState.Completed
                            : PackageUpdateState.Failed;
                        package.Progress = result.Success ? 100 : package.Progress;
                        package.ErrorMessage = result.ErrorMessage ?? string.Empty;

                        if (result.Success)
                        {
                            _allPackages.Remove(package);
                            Packages.Remove(package);
                        }
                    }
                }

                BatchCompleted = results.Count;
                SuccessfulUpdates = results.Count(result => result.Success);
                FailedUpdates = failedCount;
                UpdateProgress = 100;
                IsUpdateSummaryVisible = true;

                if (skippedCount > 0)
                {
                    StatusMessage = failedCount == 0
                        ? $"{SuccessfulUpdates} atualizações concluídas; {skippedCount} adiadas (em uso)."
                        : $"{SuccessfulUpdates} atualizações concluídas; {skippedCount} adiadas (em uso); {FailedUpdates} falharam.";
                }
                else
                {
                    StatusMessage = failedCount == 0
                        ? $"{SuccessfulUpdates} atualizações concluídas com sucesso."
                        : $"{SuccessfulUpdates} atualizações concluídas; {FailedUpdates} falharam.";
                }
            }).ConfigureAwait(false);

            _logger.LogInformation("Processo de atualização finalizado.");
            _notificationService.ShowUpdateComplete(SuccessfulUpdates, FailedUpdates);

            await RunOnUIAsync(() =>
            {
                RefreshFlyoutPackages();
                var remainingCount = _allPackages.Count(p => p.UpdateState != PackageUpdateState.Completed);
                HasUpdates = remainingCount > 0;
                NotifyCountsChanged();
                (Application.Current as App)?.UpdateTrayState(remainingCount);
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
            _cancellationTokenSource = null;
            await RunOnUIAsync(() =>
            {
                foreach (var package in pacotesParaAtualizar.Where(p => p.UpdateState is PackageUpdateState.Queued or PackageUpdateState.Updating))
                {
                    package.UpdateState = PackageUpdateState.Failed;
                }

                IsUpdating = false;
                CurrentUpdatingPackage = string.Empty;
                UpdateProgress = 0;
                NotifyCountsChanged();
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
        foreach (var package in _allPackages)
        {
            package.IsSelected = true;
        }
        UpdateSelectionState();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var package in _allPackages)
        {
            package.IsSelected = false;
        }
        UpdateSelectionState();
    }

    public void DeferPackage(PackageItemViewModel package)
    {
        if (_allPackages.Remove(package))
        {
            Packages.Remove(package);
            if (!DeferredPackages.Contains(package))
            {
                DeferredPackages.Add(package);
            }
        }
        else if (DeferredPackages.Remove(package))
        {
            _allPackages.Add(package);
            ApplyFilters();
        }

        HasUpdates = _allPackages.Count > 0;
        RefreshFlyoutPackages();
        NotifyCountsChanged();
        (Application.Current as App)?.UpdateTrayState(_allPackages.Count);
    }

    public void RemovePackageFromList(PackageItemViewModel package)
    {
        _allPackages.Remove(package);
        Packages.Remove(package);
        DeferredPackages.Remove(package);
        HasUpdates = _allPackages.Count > 0;
        RefreshFlyoutPackages();
        NotifyCountsChanged();
        (Application.Current as App)?.UpdateTrayState(_allPackages.Count);
    }

    public async Task SkipPackagePermanentlyAsync(PackageItemViewModel package)
    {
        await _packageRepository.SkipPackageAsync(package.Id).ConfigureAwait(true);
        _logger.LogInformation("Pacote {PackageId} adicionado à lista de ignorados permanentes.", package.Id);

        RemovePackageFromList(package);
    }

    /// <summary>
    /// Executa a atualização automática em segundo plano para pacotes disponíveis,
    /// pulando automaticamente qualquer aplicativo que esteja em execução com janela visível.
    /// </summary>
    public async Task ExecuteAutoUpdateAsync(CancellationToken ct = default)
    {
        var pacotesParaAtualizar = _allPackages.ToList();
        if (pacotesParaAtualizar.Count == 0 || IsUpdating)
            return;

        try
        {
            await RunOnUIAsync(() => IsUpdating = true).ConfigureAwait(false);
            using var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;

            _logger.LogInformation("Iniciando AutoUpdate em segundo plano para {Count} pacotes...", pacotesParaAtualizar.Count);

            var results = await _updateService
                .ExecuteUpdatesAsync(
                    pacotesParaAtualizar.Select(package => package.Id),
                    progress: null,
                    confirmElevation: null,
                    handleRunningApp: null,
                    isAutoUpdate: true,
                    cancellationTokenSource.Token)
                .ConfigureAwait(false);

            var failedCount = results.Count(result => !result.Success && !result.Skipped);
            var skippedCount = results.Count(result => result.Skipped);
            var successCount = results.Count(result => result.Success);

            await RunOnUIAsync(() =>
            {
                foreach (var result in results)
                {
                    var package = pacotesParaAtualizar.FirstOrDefault(p => p.Id == result.PackageId);
                    if (package is null) continue;

                    if (result.Success)
                    {
                        package.UpdateState = PackageUpdateState.Completed;
                        package.Progress = 100;
                        _allPackages.Remove(package);
                        Packages.Remove(package);
                    }
                    else if (result.Skipped)
                    {
                        package.UpdateState = PackageUpdateState.Pending;
                        package.IsSelected = false;
                        package.ErrorMessage = result.ErrorMessage ?? "Adiado para a próxima verificação (em execução).";
                    }
                    else
                    {
                        package.UpdateState = PackageUpdateState.Failed;
                        package.ErrorMessage = result.ErrorMessage ?? string.Empty;
                    }
                }

                SuccessfulUpdates = successCount;
                FailedUpdates = failedCount;
                RefreshFlyoutPackages();
                var remainingCount = _allPackages.Count(p => p.UpdateState != PackageUpdateState.Completed);
                HasUpdates = remainingCount > 0;
                NotifyCountsChanged();
                (Application.Current as App)?.UpdateTrayState(remainingCount);
            }).ConfigureAwait(false);

            if (successCount > 0)
            {
                _notificationService.ShowUpdateComplete(successCount, failedCount);
            }

            _logger.LogInformation("AutoUpdate concluído: {Success} com sucesso, {Skipped} adiados (janela visível), {Failed} com falha.",
                successCount, skippedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a execução do AutoUpdate.");
        }
        finally
        {
            _cancellationTokenSource = null;
            await RunOnUIAsync(() =>
            {
                IsUpdating = false;
                NotifyCountsChanged();
            }).ConfigureAwait(false);
        }
    }

    private void RefreshFlyoutPackages()
    {
        FlyoutPackages.Clear();
        foreach (var pkg in _allPackages)
        {
            FlyoutPackages.Add(pkg);
        }
    }

    public void ApplyFilters()
    {
        var filtered = _allPackages.Where(package =>
            (_categoryFilter == "Todos" || package.Category == _categoryFilter) &&
            (string.IsNullOrWhiteSpace(_searchText)
                || package.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || package.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase)));

        filtered = _sortBy switch
        {
            "Categoria" => filtered.OrderBy(package => CategoryOrder(package.Category))
                .ThenBy(package => package.Name, StringComparer.OrdinalIgnoreCase),
            "Fonte" => filtered.OrderBy(package => package.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
        };

        Packages.Clear();
        foreach (var package in filtered)
        {
            Packages.Add(package);
        }

        RefreshFlyoutPackages();
        NotifyCountsChanged();
    }

    private static int CategoryOrder(string category) => category switch
    {
        "App" => 0,
        "Runtime" => 1,
        "Segurança" => 2,
        _ => 3
    };
}
