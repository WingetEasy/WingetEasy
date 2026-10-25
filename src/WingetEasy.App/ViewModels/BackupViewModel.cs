using WingetEasy.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.App.ViewModels;

public sealed class BackupSnapshotItemViewModel
{
    public BackupSnapshot Snapshot { get; }
    public string Label => Snapshot.Label;
    public string CreatedAt => Snapshot.CreatedAt.ToLocalTime().ToString("g");
    public string PackageCount => LocalizationService.Instance.IsPortuguese ? $"{Snapshot.PackageCount} pacote(s)" : $"{Snapshot.PackageCount} package(s)";
    public string WingetVersion => string.IsNullOrWhiteSpace(Snapshot.WingetVersion)
        ? LocalizationService.Instance.Get("Versão desconhecida")
        : $"Winget {Snapshot.WingetVersion}";

    public BackupSnapshotItemViewModel(BackupSnapshot snapshot)
    {
        Snapshot = snapshot;
    }
}

public partial class BackupViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly IBackupSnapshotRepository _snapshotRepository;
    private readonly IProfilesService _profilesService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogger<BackupViewModel> _logger;
    private IntPtr _windowHandle;

    public ObservableCollection<BackupSnapshotItemViewModel> Snapshots { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string LabelInput { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    public partial string ProgressMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    public partial string StatusMessage { get; set; } = string.Empty;

    public Visibility IsProgressVisible => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsStatusVisible => string.IsNullOrWhiteSpace(StatusMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Visibility EmptyStateVisibility => Snapshots.Count == 0 && !IsBusy
        ? Visibility.Visible
        : Visibility.Collapsed;

    public BackupViewModel(
        IBackupService backupService,
        IBackupSnapshotRepository snapshotRepository,
        IProfilesService profilesService,
        DispatcherQueue dispatcherQueue,
        ILogger<BackupViewModel> logger)
    {
        _backupService = backupService;
        _snapshotRepository = snapshotRepository;
        _profilesService = profilesService;
        _dispatcherQueue = dispatcherQueue;
        _logger = logger;
    }

    public void InitializeFilePicker(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var snapshots = await _snapshotRepository.GetAllAsync().ConfigureAwait(true);
            Snapshots.Clear();
            foreach (var snapshot in snapshots.OrderByDescending(snapshot => snapshot.CreatedAt))
            {
                Snapshots.Add(new BackupSnapshotItemViewModel(snapshot));
            }

            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar os snapshots de backup.");
            StatusMessage = "Não foi possível carregar os snapshots salvos.";
        }
    }

    [RelayCommand]
    private async Task CreateSnapshotAsync()
    {
        try
        {
            BeginOperation("Criando snapshot...", 0);
            var label = string.IsNullOrWhiteSpace(LabelInput)
                ? $"Snapshot {DateTime.Now:dd/MM/yyyy HH:mm}"
                : LabelInput.Trim();
            var snapshot = await _backupService.CreateSnapshotAsync(label).ConfigureAwait(true);
            LabelInput = string.Empty;
            await LoadAsync().ConfigureAwait(true);
            StatusMessage = $"Snapshot \"{snapshot.Label}\" criado com {snapshot.PackageCount} pacote(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar snapshot.");
            StatusMessage = "Não foi possível criar o snapshot.";
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>
    /// Retorna os pacotes do snapshot (carrega do repositório caso a lista não esteja em memória).
    /// </summary>
    public async Task<IReadOnlyList<BackupPackage>> GetSnapshotPackagesAsync(BackupSnapshotItemViewModel item)
    {
        try
        {
            var snapshot = item.Snapshot.Packages is null
                ? await _snapshotRepository.GetAsync(item.Snapshot.Id).ConfigureAwait(true)
                : item.Snapshot;
            return snapshot?.Packages ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar os pacotes do snapshot {SnapshotId}.", item.Snapshot.Id);
            StatusMessage = "Não foi possível carregar os pacotes do snapshot.";
            return [];
        }
    }

    public async Task RestoreSelectedAsync(BackupSnapshotItemViewModel item, IReadOnlyCollection<string> packageIds)
    {
        if (packageIds.Count == 0)
        {
            StatusMessage = "Nenhum pacote selecionado para restauração.";
            return;
        }

        try
        {
            BeginOperation($"Restaurando \"{item.Label}\"...", 0);
            var progress = new Progress<(string packageId, int current, int total)>(value =>
                EnqueueOnUi(() =>
                {
                    ProgressValue = value.total == 0 ? 0 : value.current * 100d / value.total;
                    ProgressMessage = $"Restaurando {value.packageId} ({value.current} de {value.total})...";
                }));
            var results = await _backupService.RestoreSnapshotAsync(
                item.Snapshot.Id, progress, 1, CancellationToken.None, packageIds).ConfigureAwait(true);
            var succeeded = results.Count(result => result.Success);
            var failed = results.Count - succeeded;
            StatusMessage = $"Restauração concluída: {succeeded} sucesso(s) e {failed} falha(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao restaurar o snapshot {SnapshotId}.", item.Snapshot.Id);
            StatusMessage = "Não foi possível restaurar o snapshot.";
        }
        finally
        {
            EndOperation();
        }
    }

    public async Task<bool> SaveAsProfileAsync(
        string name,
        string description,
        IReadOnlyCollection<BackupPackage> packages)
    {
        try
        {
            var profilePackages = packages
                .Select(package => new ProfilePackage(package.Id, string.IsNullOrWhiteSpace(package.Name) ? package.Id : package.Name))
                .DistinctBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var profile = new Profile(Guid.Empty, name.Trim(), description.Trim(), DateTime.UtcNow, profilePackages);
            await _profilesService.SaveAsync(profile).ConfigureAwait(true);

            // Atualiza a aba Perfis imediatamente caso ela já esteja carregada.
            var profilesViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<ProfilesViewModel>(App.Services);
            if (profilesViewModel is not null)
            {
                await profilesViewModel.LoadAsync().ConfigureAwait(true);
            }

            StatusMessage = $"Perfil \"{profile.Name}\" criado com {profilePackages.Count} pacote(s). Disponível na aba Perfis.";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar perfil a partir do snapshot.");
            StatusMessage = "Não foi possível criar o perfil.";
            return false;
        }
    }
    [RelayCommand]
    private Task ExportScriptAsync(BackupSnapshotItemViewModel? item) =>
        ExportAsync(item, "Script PowerShell", ".ps1", "snapshot-restauracao", _backupService.ExportScriptAsync);

    [RelayCommand]
    private Task ExportJsonAsync(BackupSnapshotItemViewModel? item) =>
        ExportAsync(item, "Arquivo JSON", ".json", "snapshot", _backupService.ExportJsonAsync);

    [RelayCommand]
    private async Task DeleteSnapshotAsync(BackupSnapshotItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _backupService.DeleteAsync(item.Snapshot.Id.ToString()).ConfigureAwait(true);
            Snapshots.Remove(item);
            StatusMessage = $"Snapshot \"{item.Label}\" excluído.";
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir o snapshot {SnapshotId}.", item.Snapshot.Id);
            StatusMessage = "Não foi possível excluir o snapshot.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync(
        BackupSnapshotItemViewModel? item,
        string fileType,
        string extension,
        string filePrefix,
        Func<Guid, string, CancellationToken, Task> export)
    {
        if (item is null)
        {
            return;
        }

        var windowHandle = _windowHandle != IntPtr.Zero ? _windowHandle : App.MainWindowHandle;
        if (windowHandle == IntPtr.Zero)
        {
            StatusMessage = "Não foi possível abrir o seletor de arquivos: janela principal indisponível.";
            return;
        }

        try
        {
            IsBusy = true;
            var suggestedName = $"{filePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}";
            var path = await PickSavePathAsync(windowHandle, fileType, extension, suggestedName).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusMessage = "Exportação cancelada.";
                return;
            }

            await export(item.Snapshot.Id, path, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = $"Arquivo exportado com sucesso em: {path}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao exportar o snapshot {SnapshotId}.", item.Snapshot.Id);
            StatusMessage = $"Não foi possível exportar o snapshot: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task<string?> PickSavePathAsync(IntPtr windowHandle, string fileType, string extension, string suggestedName) =>
        SaveFilePicker.PickAsync(windowHandle, fileType, extension, suggestedName, _logger);

    private void BeginOperation(string message, double value)
    {
        IsBusy = true;
        ProgressMessage = message;
        ProgressValue = value;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    private void EndOperation()
    {
        IsBusy = false;
        ProgressMessage = string.Empty;
        ProgressValue = 0;
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    private void EnqueueOnUi(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }
}
