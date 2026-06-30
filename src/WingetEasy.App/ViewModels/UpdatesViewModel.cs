using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WingetEasy.Core.Interfaces;

namespace WingetEasy.App.ViewModels;

/// <summary>
/// ViewModel central responsável por gerenciar a busca e aplicação de atualizações.
/// </summary>
public partial class UpdatesViewModel : ObservableObject
{
    private readonly IWingetService _wingetService;
    private readonly ILogger<UpdatesViewModel> _logger;
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

    public UpdatesViewModel(IWingetService wingetService, ILogger<UpdatesViewModel> logger)
    {
        _wingetService = wingetService;
        _logger = logger;
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

            _cancellationTokenSource = new CancellationTokenSource();

            // Adicionado o ConfigureAwait(true) para o CA2007
            await Task.Delay(2000, _cancellationTokenSource.Token).ConfigureAwait(true);

            Packages.Clear();

            Packages.Add(new PackageItemViewModel("Microsoft.VisualStudioCode", "Visual Studio Code", "1.89.1", "1.90.0", this));
            Packages.Add(new PackageItemViewModel("Google.Chrome", "Google Chrome", "124.0.6", "125.0.6", this));
            Packages.Add(new PackageItemViewModel("Microsoft.PowerToys", "PowerToys", "0.80.1", "0.81.0", this));

            HasUpdates = Packages.Any();
            StatusMessage = HasUpdates ? $"{Packages.Count} atualizações encontradas." : "O seu sistema está totalmente atualizado!";

            UpdateSelectionState();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Verificação cancelada.";
            _logger.LogInformation("A verificação de atualizações foi cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Ocorreu um erro ao verificar as atualizações.";
            _logger.LogError(ex, "Erro fatal não tratado em CheckUpdatesAsync.");
        }
        finally
        {
            IsChecking = false;
        }
    }

    private bool CanUpdate() => SelectedCount > 0 && !IsUpdating;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateSelectedAsync()
    {
        try
        {
            IsUpdating = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var pacotesParaAtualizar = Packages.Where(p => p.IsSelected).ToList();
            _logger.LogInformation("Iniciando atualização de {Count} pacotes.", pacotesParaAtualizar.Count);

            int total = pacotesParaAtualizar.Count;
            int concluidos = 0;

            foreach (var package in pacotesParaAtualizar)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                CurrentUpdatingPackage = package.Name;
                StatusMessage = $"A atualizar {package.Name} ({concluidos + 1} de {total})...";

                // Adicionado o ConfigureAwait(true) para o CA2007
                await Task.Delay(3000, _cancellationTokenSource.Token).ConfigureAwait(true);

                concluidos++;
                UpdateProgress = (int)((double)concluidos / total * 100);
            }

            StatusMessage = "Atualizações concluídas com sucesso!";
            _logger.LogInformation("Processo de atualização finalizado.");

            Packages.Clear();
            HasUpdates = false;
            UpdateSelectionState();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Instalação cancelada em andamento.";
            _logger.LogWarning("O processo de atualização foi interrompido.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao atualizar: {CurrentUpdatingPackage}";
            _logger.LogError(ex, "Erro ao atualizar o pacote {PackageName}.", CurrentUpdatingPackage);
        }
        finally
        {
            IsUpdating = false;
            CurrentUpdatingPackage = string.Empty;
            UpdateProgress = 0;
        }
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
}
