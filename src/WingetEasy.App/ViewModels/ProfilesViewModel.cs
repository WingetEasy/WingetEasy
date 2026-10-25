using WingetEasy.App.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.App.ViewModels;

public sealed class ProfileItemViewModel
{
    public Profile Profile { get; }
    public string Name => Profile.Name;
    public string Description => Profile.Description;
    public string PackageSummary => LocalizationService.Instance.IsPortuguese ? $"{Profile.Packages.Count} pacote(s)" : $"{Profile.Packages.Count} package(s)";
    public string Packages => string.Join(", ", Profile.Packages.Select(package => package.Name));

    public ProfileItemViewModel(Profile profile) => Profile = profile;
}

public partial class ProfilesViewModel : ObservableObject
{
    private readonly IProfilesService _profilesService;
    private readonly IWingetService _wingetService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogger<ProfilesViewModel> _logger;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];
    public ObservableCollection<WingetPackage> InstalledPackages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string NameInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DescriptionInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PackageIdsInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProgressMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusVisible))]
    public partial string StatusMessage { get; set; } = string.Empty;

    public ProfileItemViewModel? EditingProfile { get; private set; }
    public Visibility IsProgressVisible => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsStatusVisible => string.IsNullOrWhiteSpace(StatusMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Visibility EmptyStateVisibility => Profiles.Count == 0 && !IsBusy
        ? Visibility.Visible
        : Visibility.Collapsed;

    public ProfilesViewModel(
        IProfilesService profilesService,
        IWingetService wingetService,
        DispatcherQueue dispatcherQueue,
        ILogger<ProfilesViewModel> logger)
    {
        _profilesService = profilesService;
        _wingetService = wingetService;
        _dispatcherQueue = dispatcherQueue;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var profiles = await _profilesService.GetAllAsync().ConfigureAwait(true);
            Profiles.Clear();
            foreach (var profile in profiles.OrderBy(profile => profile.Name))
            {
                Profiles.Add(new ProfileItemViewModel(profile));
            }

            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar os perfis.");
            StatusMessage = "Não foi possível carregar os perfis.";
        }
    }

    [RelayCommand]
    private async Task LoadInstalledPackagesAsync()
    {
        try
        {
            var packages = await _wingetService.GetInstalledPackagesAsync().ConfigureAwait(true);
            InstalledPackages.Clear();
            foreach (var package in packages.OrderBy(package => package.Name))
            {
                InstalledPackages.Add(package);
            }

            StatusMessage = $"{InstalledPackages.Count} pacote(s) instalado(s) carregado(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar os pacotes instalados.");
            StatusMessage = "Não foi possível carregar os pacotes instalados.";
        }
    }

    [RelayCommand]
    private void EditProfile(ProfileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        EditingProfile = item;
        NameInput = item.Profile.Name;
        DescriptionInput = item.Profile.Description;
        PackageIdsInput = string.Join(Environment.NewLine, item.Profile.Packages.Select(package => $"{package.Id}|{package.Name}"));
        OnPropertyChanged(nameof(EditingProfile));
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(NameInput))
        {
            StatusMessage = "Informe um nome para o perfil.";
            return;
        }

        try
        {
            var packages = PackageIdsInput
                .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Split('|', 2, StringSplitOptions.TrimEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value[0]))
                .Select(value => new ProfilePackage(value[0], value.Length > 1 ? value[1] : value[0]))
                .DistinctBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var profile = new Profile(
                EditingProfile?.Profile.Id ?? Guid.Empty,
                NameInput.Trim(),
                DescriptionInput.Trim(),
                EditingProfile?.Profile.CreatedAt ?? DateTime.UtcNow,
                packages);
            await _profilesService.SaveAsync(profile).ConfigureAwait(true);
            ClearEditor();
            await LoadAsync().ConfigureAwait(true);
            StatusMessage = $"Perfil \"{profile.Name}\" salvo com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar o perfil.");
            StatusMessage = "Não foi possível salvar o perfil.";
        }
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(ProfileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _profilesService.DeleteAsync(item.Profile.Id).ConfigureAwait(true);
            Profiles.Remove(item);
            StatusMessage = $"Perfil \"{item.Name}\" excluído.";
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir o perfil {ProfileId}.", item.Profile.Id);
            StatusMessage = "Não foi possível excluir o perfil.";
        }
    }

    [RelayCommand]
    private async Task InstallProfileAsync(ProfileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ProgressMessage = $"Instalando \"{item.Name}\"...";
            var progress = new Progress<(string packageId, int current, int total)>(value =>
                EnqueueOnUi(() =>
                {
                    ProgressValue = value.total == 0 ? 0 : value.current * 100d / value.total;
                    ProgressMessage = $"Instalando {value.packageId} ({value.current} de {value.total})...";
                }));
            var results = await _profilesService.InstallProfileAsync(item.Profile.Id, progress).ConfigureAwait(true);
            StatusMessage = $"Instalação concluída: {results.Count(result => result.Success)} sucesso(s) e {results.Count(result => !result.Success)} falha(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao instalar o perfil {ProfileId}.", item.Profile.Id);
            StatusMessage = "Não foi possível instalar o perfil.";
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
            ProgressMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelEdit() => ClearEditor();

    private void ClearEditor()
    {
        EditingProfile = null;
        NameInput = string.Empty;
        DescriptionInput = string.Empty;
        PackageIdsInput = string.Empty;
        OnPropertyChanged(nameof(EditingProfile));
    }

    private void EnqueueOnUi(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }
}
