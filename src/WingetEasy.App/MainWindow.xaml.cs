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


        public MainWindow()
        {
            this.InitializeComponent();

            // 1. Resolução do repositório através do nosso injetor central configurado na Issue 19
            _settingsRepo = App.Services.GetRequiredService<ISettingsRepository>();

            // 2. Acesso à API nativa da janela
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.ico");
            _appWindow.SetIcon(iconPath);

            _appWindow.Closing += AppWindow_Closing;
            this.Activated += MainWindow_Activated;

            // 3. Aplica o Material Mica (Win 11) ou Acrylic (Win 10)
            ConfigureBackdrop();
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
                await RestoreOrCenterWindowAsync().ConfigureAwait(true);

                // Seleciona e carrega a página de Atualizações na primeira abertura
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        }

        private async Task RestoreOrCenterWindowAsync()
        {
            var savedX = await GetWindowCoordinateAsync(WindowXKey).ConfigureAwait(true);
            var savedY = await GetWindowCoordinateAsync(WindowYKey).ConfigureAwait(true);
            var savedWidth = await GetWindowCoordinateAsync(WindowWidthKey).ConfigureAwait(true);
            var savedHeight = await GetWindowCoordinateAsync(WindowHeightKey).ConfigureAwait(true);

            if (savedX.HasValue && savedY.HasValue && savedWidth > 0 && savedHeight > 0)
            {
                _appWindow.MoveAndResize(new RectInt32(
                    savedX.Value,
                    savedY!.Value,
                    savedWidth.Value,
                    savedHeight.Value));
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

            int width = 1050;
            int height = 750;
            int x = (workArea.Width - width) / 2;
            int y = (workArea.Height - height) / 2;

            _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            // Cancela a morte da janela
            args.Cancel = true;

            var pos = _appWindow.Position;
            var size = _appWindow.Size;

            await SaveWindowStateAsync(pos, size).ConfigureAwait(true);
            TrimWorkingSet();

            // A API .Hide() nativamente esconde a janela do Alt+Tab e da Barra de Tarefas
            sender.Hide();
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
            await _settingsRepo.SetAsync(WindowXKey, position.X.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(true);
            await _settingsRepo.SetAsync(WindowYKey, position.Y.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(true);
            await _settingsRepo.SetAsync(WindowWidthKey, size.Width.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(true);
            await _settingsRepo.SetAsync(WindowHeightKey, size.Height.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(true);
        }

        private static void TrimWorkingSet()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using var process = Process.GetCurrentProcess();
            if (EmptyWorkingSet(process.Handle) == 0)
            {
                Debug.WriteLine("Não foi possível reduzir o Working Set do processo.");
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag != null)
            {
                // Mapeia o clique no menu para a página física correspondente
                // O '?' avisa o C# que esta variável pode ser nula de propósito
                Type? pageType = item.Tag.ToString() switch
                {
                    "updates" => typeof(Pages.UpdatesPage),
                    "history" => typeof(Pages.HistoryPage),
                    "backup" => typeof(Pages.BackupPage),
                    "profiles" => typeof(Pages.ProfilesPage),
                    "settings" => typeof(Pages.SettingsPage),
                    _ => null
                };

                if (pageType != null)
                {
                    // Navega e injeta a página dentro do ContentFrame
                    ContentFrame.Navigate(pageType);
                }
            }
        }

        public void FocusUpdatesPage()
        {
            if (NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        }
    }
}
