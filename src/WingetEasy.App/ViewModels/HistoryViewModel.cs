using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.App.ViewModels;

public enum HistoryPeriod
{
    Today,
    SevenDays,
    Month,
    All
}

public sealed class HistoryEntryViewModel
{
    public string PackageName { get; }
    public string PackageId { get; }
    public string VersionTransition { get; }
    public string Time { get; }
    public string StatusText { get; }
    public string StatusGlyph { get; }
    public string ErrorMessage { get; }
    public bool Success { get; }

    public HistoryEntryViewModel(UpdateResult result)
    {
        PackageName = result.PackageName;
        PackageId = result.PackageId;
        VersionTransition = $"{result.FromVersion} -> {result.ToVersion}";
        Time = (result.UpdatedAt ?? DateTime.Now.ToUniversalTime()).ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
        Success = result.Success;
        StatusText = result.Success ? "Sucesso" : "Falha";
        StatusGlyph = result.Success ? "\uE73E" : "\uE711";
        ErrorMessage = result.ErrorMessage ?? string.Empty;
    }
}

public sealed class HistoryDayGroup
{
    public string Header { get; }
    public ObservableCollection<HistoryEntryViewModel> Entries { get; } = [];

    public HistoryDayGroup(DateTime date, IEnumerable<UpdateResult> results)
    {
        Header = GetHeader(date);
        foreach (var result in results)
        {
            Entries.Add(new HistoryEntryViewModel(result));
        }
    }

    private static string GetHeader(DateTime date)
    {
        var today = DateTime.Today;
        if (date.Date == today) return "Hoje";
        if (date.Date == today.AddDays(-1)) return "Ontem";
        return date.ToString("D", CultureInfo.CurrentCulture);
    }
}

public partial class HistoryViewModel : ObservableObject
{
    private readonly IUpdateHistoryRepository _repository;
    private readonly ILogger<HistoryViewModel> _logger;
    private readonly ObservableCollection<UpdateResult> _allEntries = [];
    private IntPtr _windowHandle;
    private HistoryPeriod _selectedPeriod = HistoryPeriod.All;
    private string _searchText = string.Empty;

    public ObservableCollection<HistoryDayGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public HistoryPeriod SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value))
            {
                ApplyFilters();
            }
        }
    }

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

    public bool HasEntries => Groups.Any(group => group.Entries.Count > 0);
    public Visibility EmptyStateVisibility => HasEntries ? Visibility.Collapsed : Visibility.Visible;
    public string EmptyStateMessage => string.IsNullOrWhiteSpace(SearchText)
        ? "Nenhuma atualização encontrada para o período selecionado."
        : "Nenhuma atualização corresponde à busca.";

    public HistoryViewModel(
        IUpdateHistoryRepository repository,
        ILogger<HistoryViewModel> logger)
    {
        _repository = repository;
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
            IsLoading = true;
            var entries = await _repository.GetByDataRangeAsync(
                DateTime.MinValue,
                DateTime.UtcNow,
                CancellationToken.None).ConfigureAwait(true);

            _allEntries.Clear();
            foreach (var entry in entries)
            {
                _allEntries.Add(entry);
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusMessage = "Não foi possível carregar o histórico.";
            _logger.LogError(ex, "Erro ao carregar o histórico de atualizações.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            StatusMessage = "Não foi possível abrir o seletor de arquivos.";
            return;
        }

        try
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = $"historico-atualizacoes-{DateTime.Now:yyyyMMdd-HHmmss}",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeChoices.Add("Arquivo CSV", [".csv"]);
            InitializeWithWindow.Initialize(picker, _windowHandle);

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            var rows = Groups.SelectMany(group => group.Entries)
                .Select(entry => string.Join(';',
                    Csv(entry.PackageName),
                    Csv(entry.PackageId),
                    Csv(entry.VersionTransition),
                    Csv(entry.StatusText),
                    Csv(entry.Time),
                    Csv(entry.ErrorMessage)));
            var csv = "Pacote;ID;Versão;Status;Horário;Erro" + Environment.NewLine
                + string.Join(Environment.NewLine, rows);
            await FileIO.WriteTextAsync(file, csv).AsTask().ConfigureAwait(true);
            StatusMessage = "Histórico exportado com sucesso.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Não foi possível exportar o histórico.";
            _logger.LogError(ex, "Erro ao exportar o histórico para CSV.");
        }
    }

    private void ApplyFilters()
    {
        var start = _selectedPeriod switch
        {
            HistoryPeriod.Today => DateTime.UtcNow.Date,
            HistoryPeriod.SevenDays => DateTime.UtcNow.AddDays(-7),
            HistoryPeriod.Month => DateTime.UtcNow.AddDays(-30),
            _ => DateTime.MinValue
        };

        var filtered = _allEntries.Where(entry =>
            (entry.UpdatedAt ?? DateTime.UtcNow) >= start &&
            (string.IsNullOrWhiteSpace(_searchText)
                || entry.PackageName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || entry.PackageId.Contains(_searchText, StringComparison.OrdinalIgnoreCase)));

        Groups.Clear();
        foreach (var group in filtered
            .GroupBy(entry => (entry.UpdatedAt ?? DateTime.UtcNow).ToLocalTime().Date)
            .OrderByDescending(group => group.Key))
        {
            Groups.Add(new HistoryDayGroup(group.Key, group.OrderByDescending(entry => entry.UpdatedAt)));
        }

        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private static string Csv(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
