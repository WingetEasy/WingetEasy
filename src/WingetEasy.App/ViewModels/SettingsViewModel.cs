using WingetEasy.App.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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

    private readonly ISettingsRepository _settingsRepository;
    private readonly ISchedulerService _schedulerService;
    private readonly IPackageRepository _packageRepository;
    private readonly IWingetService _wingetService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IAppUpdateService _appUpdateService;

    // Impede que a aplicação dos valores carregados do banco dispare novas gravações/efeitos colaterais
    private bool _isLoading = true;

    public ObservableCollection<FrequencyOption> FrequencyOptions { get; } =
    [
        new FrequencyOption(ScheduleFrequency.Manual, "A cada hora"),
        new FrequencyOption(ScheduleFrequency.TwiceDaily, "A cada 6 horas"),
        new FrequencyOption(ScheduleFrequency.OnceDaily, "Diariamente"),
        new FrequencyOption(ScheduleFrequency.Weekly, "Semanalmente")
    ];

    public ObservableCollection<string> IgnoredPackages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScheduleTimeEditable))]
    [NotifyPropertyChangedFor(nameof(FrequencyIndex))]
    public partial FrequencyOption SelectedFrequencyOption { get; set; }

    [ObservableProperty]
    public partial TimeSpan CheckTime { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; } = true;

    [ObservableProperty]
    public partial bool KeepInSystemTray { get; set; } = true;

    [ObservableProperty]
    public partial bool AutoUpdate { get; set; } = false;

    [ObservableProperty]
    public partial bool NotifyBeforeUpdate { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeBetaVersions { get; set; } = false;

    [ObservableProperty]
    public partial bool SecurityAlerts { get; set; } = true;

    [ObservableProperty]
    public partial bool QuietHours { get; set; } = true;

    [ObservableProperty]
    public partial bool NotificationSound { get; set; } = false;

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
    public partial string AppUpdateStatusMessage { get; set; } = "Distribuído sob licença MIT · Não afiliado à Microsoft";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRepairingSourcesVisibility))]
    [NotifyCanExecuteChangedFor(nameof(RepairWingetSourcesCommand))]
    public partial bool IsRepairingSources { get; set; }

    [ObservableProperty]
    public partial bool IsRepairStatusVisible { get; set; }

    [ObservableProperty]
    public partial string RepairStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity RepairStatusSeverity { get; set; } = InfoBarSeverity.Informational;

    public Visibility IsRepairingSourcesVisibility => IsRepairingSources ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasIgnoredPackagesVisibility => IgnoredPackages.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool IsScheduleTimeEditable => SelectedFrequencyOption?.Value != ScheduleFrequency.Manual;

    public int FrequencyIndex
    {
        get => SelectedFrequencyOption?.Value switch
        {
            ScheduleFrequency.Manual => 0,
            ScheduleFrequency.TwiceDaily => 1,
            ScheduleFrequency.OnceDaily => 2,
            ScheduleFrequency.Weekly => 3,
            _ => 2
        };
        set
        {
            var clamped = Math.Clamp(value, 0, FrequencyOptions.Count - 1);
            SelectedFrequencyOption = FrequencyOptions[clamped];
            OnPropertyChanged(nameof(FrequencyIndex));
        }
    }

    public string AppVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string AppVersionDisplay => LocalizationService.Instance.IsPortuguese
        ? $"Versão {AppVersion} · canal estável"
        : $"Version {AppVersion} · stable channel";

    public string WingetStatusMessage => IsWingetAvailable
        ? (LocalizationService.Instance.IsPortuguese ? $"Winget instalado ({WingetVersion})" : $"Winget installed ({WingetVersion})")
        : (LocalizationService.Instance.IsPortuguese ? "Winget não foi encontrado. Instale o Windows Package Manager para habilitar as verificações." : "Winget not found. Install Windows Package Manager to enable checks.");

    public InfoBarSeverity WingetInfoBarSeverity => IsWingetAvailable ? InfoBarSeverity.Success : InfoBarSeverity.Warning;

    public event EventHandler? SettingsLoaded;

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        ISchedulerService schedulerService,
        IPackageRepository packageRepository,
        IWingetService wingetService,
        IAppUpdateService appUpdateService,
        ILogger<SettingsViewModel> logger)
    {
        _settingsRepository = settingsRepository;
        _schedulerService = schedulerService;
        _packageRepository = packageRepository;
        _wingetService = wingetService;
        _appUpdateService = appUpdateService;
        _logger = logger;

        SelectedFrequencyOption = FrequencyOptions[2]; // Diariamente
        LocalizationService.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(AppVersionDisplay));
            OnPropertyChanged(nameof(WingetStatusMessage));
            AppUpdateStatusMessage = LocalizationService.Instance.Get("Distribuído sob licença MIT · Não afiliado à Microsoft");
        };
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

            SelectedFrequencyOption = FrequencyOptions.FirstOrDefault(f => f.Value == config.Frequency) ?? FrequencyOptions[2];
            CheckTime = config.CheckTime;

            StartWithWindows = ReadStartWithWindowsFromRegistry();
            KeepInSystemTray = await ReadBoolSettingAsync("KeepInSystemTray", true).ConfigureAwait(true);
            AutoUpdate = await ReadBoolSettingAsync("AutoUpdate", false).ConfigureAwait(true);
            NotifyBeforeUpdate = await ReadBoolSettingAsync("NotifyBeforeUpdate", true).ConfigureAwait(true);
            IncludeBetaVersions = await ReadBoolSettingAsync("IncludeBetaVersions", false).ConfigureAwait(true);
            SecurityAlerts = await ReadBoolSettingAsync("SecurityAlerts", true).ConfigureAwait(true);
            QuietHours = await ReadBoolSettingAsync("QuietHours", true).ConfigureAwait(true);
            NotificationSound = await ReadBoolSettingAsync("NotificationSound", false).ConfigureAwait(true);

            var themeName = await _settingsRepository.GetAsync(App.AppThemeSettingsKey).ConfigureAwait(true);
            var theme = App.ParseElementTheme(themeName);
            ApplyThemeToApp(theme);
            ThemeIndex = ThemeToIndex(theme);

            var skippedIds = await _packageRepository.GetSkippedIdsAsync().ConfigureAwait(true);
            IgnoredPackages.Clear();
            foreach (var id in skippedIds)
            {
                IgnoredPackages.Add(id);
            }
            OnPropertyChanged(nameof(HasIgnoredPackagesVisibility));

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
            SettingsLoaded?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<bool> ReadBoolSettingAsync(string key, bool defaultValue)
    {
        var raw = await _settingsRepository.GetAsync(key).ConfigureAwait(true);
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private void SaveBoolSetting(string key, bool value)
    {
        if (_isLoading) return;
        _ = _settingsRepository.SetAsync(key, value.ToString());
    }

    partial void OnKeepInSystemTrayChanged(bool value) => SaveBoolSetting("KeepInSystemTray", value);
    partial void OnAutoUpdateChanged(bool value) => SaveBoolSetting("AutoUpdate", value);
    partial void OnNotifyBeforeUpdateChanged(bool value) => SaveBoolSetting("NotifyBeforeUpdate", value);
    partial void OnIncludeBetaVersionsChanged(bool value) => SaveBoolSetting("IncludeBetaVersions", value);
    partial void OnSecurityAlertsChanged(bool value) => SaveBoolSetting("SecurityAlerts", value);
    partial void OnQuietHoursChanged(bool value) => SaveBoolSetting("QuietHours", value);
    partial void OnNotificationSoundChanged(bool value) => SaveBoolSetting("NotificationSound", value);

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
        var appTheme = IndexToAppTheme(value);
        var theme = IndexToTheme(value);
        ApplyThemeToApp(theme);

        if (_isLoading) return;

        _ = _settingsRepository.SetAsync(App.AppThemeSettingsKey, appTheme.ToString());
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
            OnPropertyChanged(nameof(HasIgnoredPackagesVisibility));
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

    private bool CanRepairWingetSources() => !IsRepairingSources;

    [RelayCommand(CanExecute = nameof(CanRepairWingetSources))]
    private async Task RepairWingetSourcesAsync()
    {
        try
        {
            IsRepairingSources = true;
            IsRepairStatusVisible = true;
            RepairStatusSeverity = InfoBarSeverity.Informational;
            RepairStatusMessage = "Reparando e atualizando as fontes do Winget...";

            var success = await _wingetService.RepairSourcesAsync().ConfigureAwait(true);
            if (success)
            {
                RepairStatusSeverity = InfoBarSeverity.Success;
                RepairStatusMessage = "Fontes do Winget reparadas e atualizadas com sucesso.";
            }
            else
            {
                RepairStatusSeverity = InfoBarSeverity.Warning;
                RepairStatusMessage = "Não foi possível reparar as fontes do Winget. Verifique as permissões de administrador e tente novamente.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reparar as fontes do Winget na SettingsPage.");
            RepairStatusSeverity = InfoBarSeverity.Error;
            RepairStatusMessage = "Ocorreu um erro inesperado ao reparar as fontes do Winget.";
            IsRepairStatusVisible = true;
        }
        finally
        {
            IsRepairingSources = false;
        }
    }

    private bool CanCheckForAppUpdate() => !IsCheckingAppUpdate;

    [RelayCommand(CanExecute = nameof(CanCheckForAppUpdate))]
    private async Task CheckForAppUpdateAsync()
    {
        try
        {
            IsCheckingAppUpdate = true;
            AppUpdateStatusMessage = "Verificando atualizações...";

            if (!_appUpdateService.IsInstalled)
            {
                AppUpdateStatusMessage = "Atualizações automáticas estão disponíveis apenas na versão instalada.";
                return;
            }

            var update = await _appUpdateService.CheckForAppUpdateAsync().ConfigureAwait(true);
            if (update is null)
            {
                AppUpdateStatusMessage = "Você já está usando a versão mais recente.";
            }
            else
            {
                await _appUpdateService.DownloadAppUpdateAsync(update).ConfigureAwait(true);
                AppUpdateStatusMessage = $"Nova versão disponível e baixada: {update.Version}.";
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

    private static AppTheme IndexToAppTheme(int index) => index switch
    {
        0 => AppTheme.Light,
        1 => AppTheme.Dark,
        _ => AppTheme.System
    };

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

    public sealed record FrequencyOption(ScheduleFrequency Value, string Label)
    {
        public override string ToString() => Label;
    }
}
