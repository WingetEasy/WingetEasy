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
    public string PackageCount => $"{Snapshot.PackageCount} pacote(s)";
    public string WingetVersion => string.IsNullOrWhiteSpace(Snapshot.WingetVersion)
        ? "Versão desconhecida"
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
        DispatcherQueue dispatcherQueue,
        ILogger<BackupViewModel> logger)
    {
        _backupService = backupService;
        _snapshotRepository = snapshotRepository;
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

    [RelayCommand]
    private async Task RestoreSnapshotAsync(BackupSnapshotItemViewModel? item)
    {
        if (item is null)
        {
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
            var results = await _backupService.RestoreSnapshotAsync(item.Snapshot.Id, progress).ConfigureAwait(true);
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
        if (item is null || _windowHandle == IntPtr.Zero)
        {
            StatusMessage = "Não foi possível abrir o seletor de arquivos.";
            return;
        }

        try
        {
            IsBusy = true;
            var picker = new FileSavePicker
            {
                SuggestedFileName = $"{filePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeChoices.Add(fileType, [extension]);
            InitializeWithWindow.Initialize(picker, _windowHandle);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            await export(item.Snapshot.Id, file.Path, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = $"Arquivo exportado com sucesso: {file.Name}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao exportar o snapshot {SnapshotId}.", item.Snapshot.Id);
            StatusMessage = "Não foi possível exportar o snapshot.";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
