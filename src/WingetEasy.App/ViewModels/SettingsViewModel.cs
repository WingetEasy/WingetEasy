using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using Windows.System;

namespace WingetEasy.App.ViewModels;

/// <summary>
/// ViewModel responsável pelas configurações visuais e comportamentais do app.
/// Todas as propriedades persistem imediatamente ao serem alteradas (sem botão Salvar explícito).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private const string ScheduleConfigKey = "ScheduleConfig";
    private const string StartWithWindowsRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartWithWindowsValueName = "WingetEasy";
    private const string GitHubRepoUrl = "https://github.com/WingetEasy/WingetEasy";
    private const string GitHubLatestReleaseApiUrl = "https://api.github.com/repos/WingetEasy/WingetEasy/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private readonly ISettingsRepository _settingsRepository;
    private readonly ISchedulerService _schedulerService;
    private readonly IPackageRepository _packageRepository;
    private readonly IWingetService _wingetService;
    private readonly ILogger<SettingsViewModel> _logger;

    // Impede que a aplicação dos valores carregados do banco dispare novas gravações/efeitos colaterais
    private bool _isLoading = true;

    public ObservableCollection<FrequencyOption> FrequencyOptions { get; } =
    [
        new FrequencyOption(ScheduleFrequency.Manual, "Manual"),
        new FrequencyOption(ScheduleFrequency.TwiceDaily, "Duas vezes ao dia"),
        new FrequencyOption(ScheduleFrequency.OnceDaily, "Uma vez ao dia"),
        new FrequencyOption(ScheduleFrequency.Weekly, "Semanalmente")
    ];

    public ObservableCollection<string> IgnoredPackages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScheduleTimeEditable))]
    public partial FrequencyOption SelectedFrequencyOption { get; set; }

    [ObservableProperty]
    public partial TimeSpan CheckTime { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WingetStatusMessage))]
    [NotifyPropertyChangedFor(nameof(WingetInfoBarSeverity))]
    public partial bool IsWingetAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WingetStatusMessage))]
    public partial string WingetVersion { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForAppUpdateCommand))]
    public partial bool IsCheckingAppUpdate { get; set; }

    [ObservableProperty]
    public partial string AppUpdateStatusMessage { get; set; } = string.Empty;

    public bool IsScheduleTimeEditable => SelectedFrequencyOption?.Value != ScheduleFrequency.Manual;

    public string AppVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string AppVersionDisplay => $"WingetEasy v{AppVersion}";

    public string WingetStatusMessage => IsWingetAvailable
        ? $"Winget instalado (versão {WingetVersion})."
        : "Winget não foi encontrado. Instale o Windows Package Manager para habilitar as verificações de atualização.";

    public InfoBarSeverity WingetInfoBarSeverity => IsWingetAvailable ? InfoBarSeverity.Success : InfoBarSeverity.Warning;

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        ISchedulerService schedulerService,
        IPackageRepository packageRepository,
        IWingetService wingetService,
        ILogger<SettingsViewModel> logger)
    {
        _settingsRepository = settingsRepository;
        _schedulerService = schedulerService;
        _packageRepository = packageRepository;
        _wingetService = wingetService;
        _logger = logger;

        SelectedFrequencyOption = FrequencyOptions[2]; // Uma vez ao dia, alinhado ao padrão do SchedulerService
    }

    /// <summary>
    /// Carrega todos os valores persistidos do banco de dados e do registro do Windows.
    /// Deve ser chamado assim que a SettingsPage é exibida.
    /// </summary>
    public async Task InitializeAsync()
    {
        _isLoading = true;
        try
        {
            var config = await _settingsRepository.GetAsync<ScheduleConfig>(ScheduleConfigKey).ConfigureAwait(true)
                ?? new ScheduleConfig(ScheduleFrequency.OnceDaily, TimeSpan.Zero);

            SelectedFrequencyOption = FrequencyOptions.First(f => f.Value == config.Frequency);
            CheckTime = config.CheckTime;

            StartWithWindows = ReadStartWithWindowsFromRegistry();

            var themeName = await _settingsRepository.GetAsync(App.AppThemeSettingsKey).ConfigureAwait(true);
            var theme = Enum.TryParse<ElementTheme>(themeName, out var parsedTheme) ? parsedTheme : ElementTheme.Default;
            ApplyThemeToApp(theme); // Garante a aplicação mesmo se o índice calculado não mudar (ex.: já é 0)
            ThemeIndex = ThemeToIndex(theme);

            var skippedIds = await _packageRepository.GetSkippedIdsAsync().ConfigureAwait(true);
            IgnoredPackages.Clear();
            foreach (var id in skippedIds)
            {
                IgnoredPackages.Add(id);
            }

            IsWingetAvailable = await _wingetService.IsWingetAvailableAsync().ConfigureAwait(true);
            WingetVersion = IsWingetAvailable ? await _wingetService.GetWingetVersionAsync().ConfigureAwait(true) : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar as configurações da SettingsPage.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnSelectedFrequencyOptionChanged(FrequencyOption value) => _ = PersistScheduleAsync();

    partial void OnCheckTimeChanged(TimeSpan value) => _ = PersistScheduleAsync();

    private async Task PersistScheduleAsync()
    {
        if (_isLoading) return;

        try
        {
            var config = new ScheduleConfig(SelectedFrequencyOption.Value, CheckTime);
            await _schedulerService.UpdateScheduleAsync(config).ConfigureAwait(true);
            _logger.LogInformation("Agendamento atualizado via SettingsPage: {Frequency} às {CheckTime}.", config.Frequency, config.CheckTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar o agendamento de verificação a partir da SettingsPage.");
        }
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoading) return;

        try
        {
            SetStartWithWindowsInRegistry(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar a entrada de inicialização automática no registro.");
        }
    }

    partial void OnThemeIndexChanged(int value)
    {
        var theme = IndexToTheme(value);
        ApplyThemeToApp(theme);

        if (_isLoading) return;

        _ = _settingsRepository.SetAsync(App.AppThemeSettingsKey, theme.ToString());
    }

    [RelayCommand]
    private void RemoveIgnoredPackage(string packageId)
    {
        if (string.IsNullOrEmpty(packageId)) return;

        _ = RemoveIgnoredPackageAsync(packageId);
    }

    private async Task RemoveIgnoredPackageAsync(string packageId)
    {
        try
        {
            await _packageRepository.UnskipPackageAsync(packageId).ConfigureAwait(true);
            IgnoredPackages.Remove(packageId);
            _logger.LogInformation("Exclusão removida para o pacote {PackageId}.", packageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover a exclusão do pacote {PackageId}.", packageId);
        }
    }

    [RelayCommand]
    private async Task OpenGitHubAsync()
    {
        await Launcher.LaunchUriAsync(new Uri(GitHubRepoUrl)).AsTask().ConfigureAwait(true);
    }

    private bool CanCheckForAppUpdate() => !IsCheckingAppUpdate;

    [RelayCommand(CanExecute = nameof(CanCheckForAppUpdate))]
    private async Task CheckForAppUpdateAsync()
    {
        try
        {
            IsCheckingAppUpdate = true;
            AppUpdateStatusMessage = "Verificando atualizações...";

            using var response = await HttpClient.GetAsync(GitHubLatestReleaseApiUrl).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                AppUpdateStatusMessage = "Não foi possível verificar atualizações no momento.";
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(true);

            var latestTag = doc.RootElement.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var latestVersionText = latestTag?.TrimStart('v', 'V');

            if (Version.TryParse(latestVersionText, out var latestVersion) && Version.TryParse(AppVersion, out var currentVersion))
            {
                AppUpdateStatusMessage = latestVersion > currentVersion
                    ? $"Nova versão disponível: {latestTag}"
                    : "Você já está usando a versão mais recente.";
            }
            else
            {
                AppUpdateStatusMessage = "Não foi possível determinar a versão mais recente.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar atualizações do WingetEasy.");
            AppUpdateStatusMessage = "Erro ao verificar atualizações. Verifique sua conexão.";
        }
        finally
        {
            IsCheckingAppUpdate = false;
        }
    }

    private static void ApplyThemeToApp(ElementTheme theme)
    {
        if (Microsoft.UI.Xaml.Application.Current is App app)
        {
            app.ApplyTheme(theme);
        }
    }

    private static ElementTheme IndexToTheme(int index) => index switch
    {
        0 => ElementTheme.Light,
        1 => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private static int ThemeToIndex(ElementTheme theme) => theme switch
    {
        ElementTheme.Light => 0,
        ElementTheme.Dark => 1,
        _ => 2
    };

    private static bool ReadStartWithWindowsFromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartWithWindowsRunKey, writable: false);
        return key?.GetValue(StartWithWindowsValueName) != null;
    }

    private static void SetStartWithWindowsInRegistry(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartWithWindowsRunKey, writable: true);
        if (key == null) return;

        if (enable)
        {
            var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            key.SetValue(StartWithWindowsValueName, $"\"{exePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(StartWithWindowsValueName, throwOnMissingValue: false);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WingetEasy", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public sealed record FrequencyOption(ScheduleFrequency Value, string Label)
    {
        public override string ToString() => Label;
    }
}
