using WingetEasy.App.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Microsoft.Extensions.DependencyInjection;
using WingetEasy.Core.Interfaces;
using WingetEasy.App.Views;
using WingetEasy.App.ViewModels;
using WingetEasy.Core.Models;

namespace WingetEasy.App
{
    public sealed partial class MainWindow : Window
    {
        private const string WindowXKey = "WindowX";
        private const string WindowYKey = "WindowY";
        private const string WindowWidthKey = "WindowWidth";
        private const string WindowHeightKey = "WindowHeight";

        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hProcess);

        private AppWindow _appWindow;
        private readonly ISettingsRepository _settingsRepo;
        private bool _isFirstActivation = true;
        private bool _onboardingShown;
        private readonly IAppUpdateService _appUpdateService;
        private AppUpdateInfo? _availableAppUpdate;
        private string _activeTag = "updates";
        private string _currentLang = "pt";

        public UpdatesViewModel UpdatesVM { get; }

        public MainWindow()
        {
            UpdatesVM = App.Services.GetRequiredService<UpdatesViewModel>();
            this.InitializeComponent();
            ContentFrame.CacheSize = 5;

            _settingsRepo = App.Services.GetRequiredService<ISettingsRepository>();
            _appUpdateService = App.Services.GetRequiredService<IAppUpdateService>();

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }

            try
            {
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = _appWindow.TitleBar;
                    titleBar.ExtendsContentIntoTitleBar = true;
                    titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
                    titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(24, 128, 128, 128);
                    titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(40, 128, 128, 128);
                }
                else
                {
                    ExtendsContentIntoTitleBar = true;
                }

                SetTitleBar(TitleBarDragRegion);
            }
            catch
            {
                CaptionButtonsSpacer.Width = 0;
            }

            _appWindow.Closing += AppWindow_Closing;
            this.Activated += MainWindow_Activated;

            ConfigureBackdrop();
            UpdateThemeToggleGlyph();
            _currentLang = LocalizationService.Instance.CurrentLanguage;
            ApplyLanguageVisuals(_currentLang);
            LocalizationService.Instance.LanguageChanged += () =>
            {
                ApplyLanguageVisuals(LocalizationService.Instance.CurrentLanguage);
                LocalizeActivePage();
            };
        }

        private async Task ShowOnboardingIfNeededAsync()
        {
            if (_onboardingShown)
            {
                return;
            }

            _onboardingShown = true;
            var completed = await _settingsRepo.GetAsync("OnboardingCompleted").ConfigureAwait(true);
            if (string.Equals(completed, "true", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Content is FrameworkElement rootElement && !rootElement.IsLoaded)
            {
                var loadedTcs = new TaskCompletionSource();
                RoutedEventHandler? onLoaded = null;
                onLoaded = (_, _) =>
                {
                    rootElement.Loaded -= onLoaded;
                    loadedTcs.TrySetResult();
                };
                rootElement.Loaded += onLoaded;
                await loadedTcs.Task.ConfigureAwait(true);
            }

            var dialog = new OnboardingDialog
            {
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync().AsTask().ConfigureAwait(true);
        }

        private void ConfigureBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };
            }
            else if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_isFirstActivation)
            {
                _isFirstActivation = false;
                try
                {
                    await RestoreOrCenterWindowAsync().ConfigureAwait(true);

                    NavigateToTag(_activeTag);
                    UpdateThemeToggleGlyph();
                    await ShowOnboardingIfNeededAsync().ConfigureAwait(true);
                    _ = CheckForAppUpdateInBackgroundAsync();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "Erro durante ativação inicial da MainWindow.");
                    Serilog.Log.CloseAndFlush();
                    throw;
                }
            }
        }

        private async void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var isCurrentlyDark = RootShellGrid.ActualTheme == ElementTheme.Dark;
            var nextTheme = isCurrentlyDark ? ElementTheme.Light : ElementTheme.Dark;
            var nextAppTheme = isCurrentlyDark ? AppTheme.Light : AppTheme.Dark;

            if (Application.Current is App app)
            {
                app.ApplyTheme(nextTheme);
            }

            UpdateThemeToggleGlyph(nextTheme);

            try
            {
                await _settingsRepo.SetAsync(App.AppThemeSettingsKey, nextAppTheme.ToString()).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao persistir tema: {ex}");
            }
        }

        public void UpdateThemeToggleGlyph(ElementTheme? forcedTheme = null)
        {
            var effective = forcedTheme ?? RootShellGrid.ActualTheme;
            if (effective == ElementTheme.Default)
            {
                effective = Application.Current.RequestedTheme == ApplicationTheme.Dark
                    ? ElementTheme.Dark
                    : ElementTheme.Light;
            }

            var isDark = effective == ElementTheme.Dark;
            ThemeToggleIcon.Glyph = isDark ? "\uE706" : "\uE708";

            if (AppWindowTitleBar.IsCustomizationSupported() && _appWindow?.TitleBar != null)
            {
                _appWindow.TitleBar.ButtonForegroundColor = isDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
                _appWindow.TitleBar.ButtonHoverForegroundColor = isDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                NavigateToTag(tag);
            }
        }

        private void NavigateToTag(string tag)
        {
            _activeTag = tag;

            SetNavVisualState(NavUpdatesButton, BarUpdates, LblUpdates, tag == "updates");
            SetNavVisualState(NavHistoryButton, BarHistory, LblHistory, tag == "history");
            SetNavVisualState(NavBackupButton, BarBackup, LblBackup, tag == "backup");
            SetNavVisualState(NavProfilesButton, BarProfiles, LblProfiles, tag == "profiles");
            SetNavVisualState(NavSettingsButton, BarSettings, LblSettings, tag == "settings");

            Type? pageType = tag switch
            {
                "updates" => typeof(Pages.UpdatesPage),
                "history" => typeof(Pages.HistoryPage),
                "backup" => typeof(Pages.BackupPage),
                "profiles" => typeof(Pages.ProfilesPage),
                "settings" => typeof(Pages.SettingsPage),
                _ => null
            };

            if (pageType != null && (ContentFrame.CurrentSourcePageType != pageType || ContentFrame.Content == null))
            {
                ContentFrame.Navigate(pageType);
                ContentFrame.BackStack.Clear();
                if (ContentFrame.Content is FrameworkElement fe)
                {
                    fe.Loaded += (_, _) => LocalizationService.Instance.LocalizeElement(fe);
                    LocalizationService.Instance.LocalizeElement(fe);
                }
                _ = Task.Run(async () => { await Task.Delay(400).ConfigureAwait(false); TrimWorkingSet(); });
            }
        }

        private void SetNavVisualState(Button button, Border indicatorBar, TextBlock label, bool isActive)
        {
            indicatorBar.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            label.FontWeight = isActive
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;
            button.Background = isActive
                ? (Brush)Application.Current.Resources["NavItemActiveBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        private void LangPtButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage("pt");
        }

        private void LangEnButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage("en");
        }

        private void ApplyLanguage(string lang)
        {
            _currentLang = lang;
            LocalizationService.Instance.SetLanguage(lang, persist: true);
        }

        private void ApplyLanguageVisuals(string lang)
        {
            _currentLang = lang;
            bool isPt = lang == "pt";

            LangPtButton.Background = isPt
                ? (Brush)Application.Current.Resources["SegmentedThumbBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            LangPtButton.Foreground = isPt
                ? (Brush)Application.Current.Resources["BrandTextPrimaryBrush"]
                : (Brush)Application.Current.Resources["BrandTextSecondaryBrush"];
            LangPtButton.FontWeight = isPt
                ? Microsoft.UI.Text.FontWeights.Bold
                : Microsoft.UI.Text.FontWeights.SemiBold;

            LangEnButton.Background = !isPt
                ? (Brush)Application.Current.Resources["SegmentedThumbBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            LangEnButton.Foreground = !isPt
                ? (Brush)Application.Current.Resources["BrandTextPrimaryBrush"]
                : (Brush)Application.Current.Resources["BrandTextSecondaryBrush"];
            LangEnButton.FontWeight = !isPt
                ? Microsoft.UI.Text.FontWeights.Bold
                : Microsoft.UI.Text.FontWeights.SemiBold;

            LibraryHeaderText.Text = isPt ? "BIBLIOTECA" : "LIBRARY";
            LblUpdates.Text = isPt ? "Atualizações" : "Updates";
            LblHistory.Text = isPt ? "Histórico" : "History";
            LblBackup.Text = "Backup";
            LblProfiles.Text = isPt ? "Perfis" : "Profiles";
            LblSettings.Text = isPt ? "Configurações" : "Settings";
            ToolTipService.SetToolTip(ThemeToggleButton, isPt ? "Alternar tema Claro / Escuro" : "Toggle Light / Dark theme");
        }

        private void LocalizeActivePage()
        {
            if (ContentFrame.Content is DependencyObject page)
            {
                LocalizationService.Instance.LocalizeElement(page);
            }
        }

        private async Task CheckForAppUpdateInBackgroundAsync()
        {
            try
            {
                _availableAppUpdate = await _appUpdateService.CheckForAppUpdateAsync().ConfigureAwait(true);
                if (_availableAppUpdate is null)
                {
                    return;
                }

                await _appUpdateService.DownloadAppUpdateAsync(_availableAppUpdate).ConfigureAwait(true);
                AppUpdateInfo? update = _availableAppUpdate;
                AppUpdateInfoBar.Message = $"A versão {update.Version} está pronta para instalar.";
                AppUpdateInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao verificar atualização do WingetEasy: {ex}");
            }
        }

        private void ApplyAppUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_availableAppUpdate is null)
            {
                return;
            }

            _appUpdateService.ApplyUpdateAndRestart(_availableAppUpdate);
        }

        private async Task RestoreOrCenterWindowAsync()
        {
            var bounds = (await _settingsRepo.GetAsync("WindowBounds").ConfigureAwait(true))?.Split(',');
            if (bounds is { Length: 4 } && int.TryParse(bounds[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) && int.TryParse(bounds[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) && int.TryParse(bounds[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) && int.TryParse(bounds[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && w > 0 && h > 0)
            {
                _appWindow.MoveAndResize(new RectInt32(x, y, w, h));
            }
            else
            {
                CenterWindowOnScreen();
            }
        }

        private void CenterWindowOnScreen()
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            int width = 1080;
            int height = 760;
            int x = (workArea.Width - width) / 2;
            int y = (workArea.Height - height) / 2;

            _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;

            var pos = _appWindow.Position;
            var size = _appWindow.Size;

            sender.Hide();
            await SaveWindowStateAsync(pos, size).ConfigureAwait(true);
            if (bool.TryParse(await _settingsRepo.GetAsync("KeepInSystemTray").ConfigureAwait(true), out var keepInTray) && !keepInTray)
            {
                Serilog.Log.CloseAndFlush();
                Environment.Exit(0);
                return;
            }
            await Task.Delay(150).ConfigureAwait(true);
            TrimWorkingSet();
        }

        public void RestoreFromTray()
        {
            if (this.SystemBackdrop == null) ConfigureBackdrop();
            if (ContentFrame.Content == null) NavigateToTag(_activeTag);
            _ = Task.Run(async () => { await Task.Delay(1000).ConfigureAwait(false); TrimWorkingSet(); });
        }

        private async Task<int?> GetWindowCoordinateAsync(string key)
        {
            var value = await _settingsRepo.GetAsync(key).ConfigureAwait(true);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var coordinate)
                ? coordinate
                : null;
        }

        private async Task SaveWindowStateAsync(PointInt32 position, SizeInt32 size)
        {
            await _settingsRepo.SetAsync("WindowBounds", string.Create(CultureInfo.InvariantCulture, $"{position.X},{position.Y},{size.Width},{size.Height}")).ConfigureAwait(true);
        }

        private static void TrimWorkingSet()
        {
            App.OptimizeBackgroundMemory();
        }

        public void FocusUpdatesPage()
        {
            NavigateToTag("updates");
        }

        public void FocusSettingsPage()
        {
            NavigateToTag("settings");
        }
    }
}
