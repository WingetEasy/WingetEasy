using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using WingetEasy.Data;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Core.Services;
using WingetEasy.App.Services;
using WingetEasy.App.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Serilog;
using Serilog.Events;
using Velopack;

namespace WingetEasy.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hProcess);

        private Window? _window;
        private Views.UpdateFlyout? _updateFlyout;
        private H.NotifyIcon.TaskbarIcon? _trayIcon;
        private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();
        private bool? _lastTrayHasUpdates = false;
        private ElementTheme _currentTheme = ElementTheme.Default;
        public static IntPtr MainWindowHandle { get; private set; }

        /// <summary>
        /// Provedor central estático exposto para toda a aplicação obter dependências.
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        /// <summary>
        /// Chave utilizada para persistir a preferência de tema no ISettingsRepository.
        /// </summary>
        public const string AppThemeSettingsKey = "AppTheme";

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            var args = Environment.GetCommandLineArgs();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logPath = System.IO.Path.Combine(appData, "WingetEasy", "Logs", "app-.log");
            var isDev = args.Contains("--dev");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(isDev ? LogEventLevel.Debug : LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("==== Inicializando WingetEasy ====");
            try
            {
                VelopackApp.Build().Run();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Velopack não está ativo neste ambiente; continuando inicialização normal.");
            }

            if (args.Contains("--elevated"))
            {
                Log.Information("Iniciando processo filho com privilégios de Administrador (UAC).");
                RunElevatedModeAndExit(args);
            }

            if (!args.Contains("--no-elevate") && !args.Contains("--minimized") && !args.Contains("--check") && !args.Contains("--update-all") && !args.Contains("--help") && !args.Contains("--version") && !new AdminElevationHelper().IsRunningAsAdmin())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? throw new InvalidOperationException(),
                        Arguments = string.Join(" ", args.Skip(1).Select(a => $"\"{a}\"")),
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    Log.CloseAndFlush();
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Elevação inicial recusada; continuando em modo padrão.");
                }
            }

            InitializeComponent();
            this.UnhandledException += (_, e) =>
            {
                Log.Fatal(e.Exception, "Exceção não tratada na UI: {Message}", e.Message);
                Log.CloseAndFlush();
            };

            Services = ConfigureServices();
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        }

        private static void RunElevatedModeAndExit(string[] args)
        {
            try
            {
                var jobIdx = Array.IndexOf(args, "--job") + 1;
                if (jobIdx > 0 && jobIdx < args.Length)
                {
                    var jobFile = args[jobIdx];
                    if (File.Exists(jobFile))
                    {
                        var json = File.ReadAllText(jobFile);
                        var job = JsonSerializer.Deserialize(json, Core.Models.CoreJsonContext.Default.ElevatedJob);

                        if (job != null && job.PackageIds != null && job.PackageIds.Any())
                        {
                            var results = new List<Core.Models.UpdateResult>();
                            foreach (var id in job.PackageIds)
                            {
                                var sw = Stopwatch.StartNew();

                                using var process = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "winget.exe",
                                        Arguments = $"upgrade --id {id} --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity",
                                        UseShellExecute = false,
                                        CreateNoWindow = true
                                    }
                                };
                                process.Start();
                                process.WaitForExit();
                                sw.Stop();

                                bool success = process.ExitCode == 0;

                                if (!success)
                                {
                                    Log.Error("Falha ao atualizar pacote via UAC: {PackageId} (ExitCode: {Code})", id, process.ExitCode);
                                }
                                else
                                {
                                    Log.Information("Pacote atualizado com sucesso via UAC: {PackageId}", id);
                                }
                                results.Add(new Core.Models.UpdateResult(id, id, success, success ? null : $"Erro {process.ExitCode}", sw.Elapsed));
                            }

                            var resultFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(jobFile) ?? string.Empty, System.IO.Path.GetFileName(jobFile).Replace("job", "result", StringComparison.Ordinal));
                            File.WriteAllText(resultFile, JsonSerializer.Serialize(results, Core.Models.CoreJsonContext.Default.ListUpdateResult));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Erro fatal não tratado no processo filho elevado.");
            }
            finally
            {
                Log.CloseAndFlush();
                Environment.Exit(0);
            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton(DispatcherQueue.GetForCurrentThread());

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(dispose: true);
            });

            services.AddDbContextFactory<AppDbContext>();

            // Repositórios injetados com escopo AddScoped
            services.AddSingleton<ISettingsRepository>(sp => new WingetEasy.Data.Repositories.SettingsRepository(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
            services.AddSingleton<IUpdateHistoryRepository>(sp => new WingetEasy.Data.Repositories.UpdateHistoryRepository(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
            services.AddSingleton<IBackupSnapshotRepository>(sp => new WingetEasy.Data.Repositories.BackupSnapshotRepository(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
            services.AddSingleton<IProfileRepository, WingetEasy.Data.Repositories.ProfileRepository>();
            services.AddSingleton<IPackageRepository>(sp => new WingetEasy.Data.Repositories.PackageRepository(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));
            services.AddSingleton<ICheckLogRepository>(sp => new WingetEasy.Data.Repositories.CheckLogRepository(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()));

            // Alinhamento das regras de arquitetura: Serviços principais como Singletons
            services.AddSingleton<IWingetService, WingetEasy.Core.Services.WingetService>();
            services.AddSingleton<IUpdateService, WingetEasy.Core.Services.UpdateService>();
            services.AddSingleton<ITaskSchedulerProcessRunner, WindowsTaskSchedulerProcessRunner>();
            services.AddSingleton<ITaskSchedulerService, WindowsTaskSchedulerService>();
            services.AddTransient<IBackupService, WingetEasy.Core.Services.BackupService>();
            services.AddTransient<IProfilesService, WingetEasy.Core.Services.ProfilesService>();
            services.AddSingleton<CliArgumentsParser>();
            services.AddTransient<HeadlessRunner>();
            services.AddSingleton<ISchedulerService, WingetEasy.Core.Services.SchedulerService>();
            services.AddSingleton<INotificationService, ToastNotificationService>();
            services.AddSingleton<IToastNotificationService>(serviceProvider =>
                (IToastNotificationService)serviceProvider.GetRequiredService<INotificationService>());
            services.AddSingleton<IAdminElevationHelper, WingetEasy.Core.Services.AdminElevationHelper>();
            services.AddSingleton<IAppUpdateManager, VelopackAppUpdateManager>();
            services.AddSingleton<IAppUpdateService, VelopackAppUpdateService>();

            // Mantém a lista de atualizações compartilhada entre as superfícies da aplicação
            services.AddSingleton<UpdatesViewModel>();
            services.AddTransient<HistoryViewModel>();
            services.AddSingleton<BackupViewModel>();
            services.AddSingleton<ProfilesViewModel>();
            services.AddTransient<SettingsViewModel>();

            return services.BuildServiceProvider();
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                var commandArgs = Environment.GetCommandLineArgs();
                var parser = Services.GetRequiredService<CliArgumentsParser>();
                if (!parser.TryParse(commandArgs, out var command, out var parseError))
                {
                    Log.Error("Falha ao interpretar argumentos CLI: {Error}", parseError);
                    Log.CloseAndFlush();
                    Environment.Exit(1);
                    return;
                }

                if (command.IsHeadless)
                {
                    await MigrateDatabaseAsync(Services).ConfigureAwait(true);
                    using var scope = Services.CreateScope();
                    var runner = scope.ServiceProvider.GetRequiredService<HeadlessRunner>();
                    var exitCode = await runner.RunAsync(command).ConfigureAwait(true);
                    Log.Information("Comando headless {CommandType} finalizado com código {ExitCode}.", command.Type, exitCode);
                    Log.CloseAndFlush();
                    Environment.Exit(exitCode);
                    return;
                }

                await MigrateDatabaseAsync(Services).ConfigureAwait(true);
                _uiSettings.ColorValuesChanged += OnColorValuesChanged;
                var isMinimized = command.Type == CliCommandType.Minimized;
                if (!isMinimized)
                {
                    await ApplySavedThemeAsync().ConfigureAwait(true);
                }

                var scheduler = Services.GetRequiredService<ISchedulerService>();
                await scheduler.StartAsync().ConfigureAwait(true);
                Services.GetRequiredService<IToastNotificationService>().Register();
                scheduler.CheckRequested += (_, _) =>
                {
                    var updatesViewModel = Services.GetRequiredService<UpdatesViewModel>();
                    updatesViewModel.CheckUpdatesCommand.Execute(null);
                };

                InitializeTrayIcon();

                var initialUpdatesVm = Services.GetRequiredService<UpdatesViewModel>();
                await initialUpdatesVm.CheckUpdatesCommand.ExecuteAsync(null).ConfigureAwait(true);

                if (isMinimized)
                {
                    OptimizeBackgroundMemory();
                    Log.Information("WingetEasy carregado silenciosamente via parâmetro --minimized.");
                }
                else
                {
                    Log.Information("Inicialização padrão detectada. Abrindo janela principal.");
                    ShowMainWindow();
                    _ = Task.Run(async () => { await Task.Delay(1200).ConfigureAwait(false); OptimizeBackgroundMemory(); });
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ocorreu um erro fatal durante o provisionamento de infraestrutura da UI.");
                Log.CloseAndFlush();
                Environment.Exit(1);
                throw;
            }
        }

        public static void OptimizeBackgroundMemory()
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            using var process = Process.GetCurrentProcess();
            if (EmptyWorkingSet(process.Handle) == 0)
            {
                Log.Debug("Não foi possível reduzir o working set do processo em modo minimizado.");
            }
        }

        private void InitializeTrayIcon()
        {
            var menuVerificar = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Verificar agora", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE72C" } };
            menuVerificar.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
            {
                try
                {
                    Log.Information("Usuário clicou em Verificar Agora.");
                    var updatesViewModel = Services.GetRequiredService<UpdatesViewModel>();
                    updatesViewModel.CheckUpdatesCommand.Execute(null);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Não foi possível iniciar a verificação de atualizações pela bandeja.");
                }
            });

            var menuAtualizacoes = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Atualizações disponíveis (0)", Visibility = Microsoft.UI.Xaml.Visibility.Collapsed, Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE895" } };
            menuAtualizacoes.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindowAndFocusUpdates);

            var menuFlyoutPainel = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Painel rápido", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE8A7" } };
            menuFlyoutPainel.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ToggleUpdateFlyout);

            var menuConfig = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Configurações", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE713" } };
            menuConfig.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindowAndFocusSettings);

            var menuHistorico = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Histórico", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE81C" } };
            menuHistorico.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow);

            var menuSobre = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Sobre", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE946" } };
            menuSobre.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindowAndFocusSettings);

            var menuSair = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Sair", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE7E8" } };
            menuSair.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
            {
                Log.Information("Usuário solicitou encerramento via System Tray. Finalizando...");
                Log.CloseAndFlush();
                _trayIcon?.Dispose();
                Environment.Exit(0);
            });

            var contextMenu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
            contextMenu.Items.Add(menuVerificar);
            contextMenu.Items.Add(menuAtualizacoes);
            contextMenu.Items.Add(menuFlyoutPainel);
            contextMenu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
            contextMenu.Items.Add(menuConfig);
            contextMenu.Items.Add(menuHistorico);
            contextMenu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
            contextMenu.Items.Add(menuSobre);
            contextMenu.Items.Add(menuSair);

            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "WingetEasy-Logo.ico");

            _trayIcon = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = "WingetEasy — Sistema atualizado",
                IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath)),
                ContextFlyout = contextMenu,
                LeftClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ToggleUpdateFlyout)
            };

            try
            {
                _trayIcon.ForceCreate();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Não foi possível registrar o ícone na bandeja do sistema.");
            }
        }

        /// <summary>
        /// Atualiza o estado visual da bandeja do sistema (Ícone, ToolTip e Menu) com base no número de atualizações.
        /// </summary>
        public void UpdateTrayState(int updateCount)
        {
            if (_trayIcon == null) return;

            bool hasUpdates = updateCount > 0;
            _trayIcon.ToolTipText = hasUpdates
                ? $"WingetEasy — {updateCount} atualizações disponíveis"
                : "WingetEasy — Sistema atualizado";

            if (_lastTrayHasUpdates != hasUpdates)
            {
                _lastTrayHasUpdates = hasUpdates;
                var fileName = hasUpdates ? "WingetEasy-Logo-update.ico" : "WingetEasy-Logo.ico";
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", fileName);

                _trayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
            }

            if (_trayIcon.ContextFlyout is Microsoft.UI.Xaml.Controls.MenuFlyout menuFlyout)
            {
                if (menuFlyout.Items[1] is Microsoft.UI.Xaml.Controls.MenuFlyoutItem menuAtualizacoes)
                {
                    menuAtualizacoes.Visibility = hasUpdates ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
                    menuAtualizacoes.Text = $"Atualizações disponíveis ({updateCount})";
                }
            }
        }

        public void ToggleUpdateFlyout()
        {
            try
            {
                if (_updateFlyout != null)
                {
                    _updateFlyout.Close();
                    _updateFlyout = null;
                    return;
                }

                _updateFlyout = new Views.UpdateFlyout();
                _updateFlyout.Activate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falha ao abrir UpdateFlyout; abrindo MainWindow como fallback.");
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            try
            {
                if (_window == null)
                {
                    _window = new MainWindow();
                    MainWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                    _window.Closed += (s, e) => _window = null;

                    _ = ApplySavedThemeAsync();
                }
                else if (_window is MainWindow existingMain)
                {
                    existingMain.RestoreFromTray();
                }
                _window.Activate();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Falha ao abrir ou ativar a MainWindow.");
                Log.CloseAndFlush();
                throw;
            }
        }

        public void ShowMainWindowAndFocusUpdates()
        {
            ShowMainWindow();
            (_window as MainWindow)?.FocusUpdatesPage();
        }

        public void ShowMainWindowAndFocusSettings()
        {
            ShowMainWindow();
            (_window as MainWindow)?.FocusSettingsPage();
        }

        /// <summary>
        /// Registra a janela UpdateFlyout ativa para que receba mudanças de tema em tempo real.
        /// </summary>
        public void RegisterUpdateFlyout(Views.UpdateFlyout flyout)
        {
            _updateFlyout = flyout;
            flyout.Closed += (_, _) =>
            {
                if (ReferenceEquals(_updateFlyout, flyout))
                {
                    _updateFlyout = null;
                }
            };

            ApplyTheme(_currentTheme);
        }

        private void OnNotificationInvoked(
            AppNotificationManager sender,
            AppNotificationActivatedEventArgs args)
        {
            var action = args.Arguments.TryGetValue("action", out var requestedAction)
                ? requestedAction
                : "viewUpdates";
            var dispatcherQueue = Services.GetRequiredService<DispatcherQueue>();

            dispatcherQueue.TryEnqueue(() =>
            {
                ShowMainWindow();
                (_window as MainWindow)?.FocusUpdatesPage();

                if (action == "updateAll")
                {
                    Services.GetRequiredService<UpdatesViewModel>()
                        .UpdateSelectedCommand.Execute(null);
                }
            });
        }

        private void OnColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
        {
            var dispatcherQueue = Services.GetRequiredService<DispatcherQueue>();
            dispatcherQueue.TryEnqueue(() =>
            {
                ApplyTheme(_currentTheme);
            });
        }

        /// <summary>
        /// Aplica de imediato o tema (Claro/Escuro/Sistema) ao elemento raiz da MainWindow e do UpdateFlyout.
        /// </summary>
        public void ApplyTheme(ElementTheme theme)
        {
            _currentTheme = theme;
            var dispatcherQueue = Services.GetRequiredService<DispatcherQueue>();

            void ApplyToRoots()
            {
                if (_window?.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = theme;
                }

                if (_window is MainWindow mainWin)
                {
                    mainWin.UpdateThemeToggleGlyph(theme);
                }

                if (_updateFlyout?.Content is FrameworkElement flyoutRoot)
                {
                    flyoutRoot.RequestedTheme = theme;
                }
            }

            if (dispatcherQueue.HasThreadAccess)
            {
                ApplyToRoots();
            }
            else
            {
                dispatcherQueue.TryEnqueue(ApplyToRoots);
            }
        }

        public static ElementTheme ParseElementTheme(string? themeName) => themeName switch
        {
            nameof(AppTheme.Light) or nameof(ElementTheme.Light) => ElementTheme.Light,
            nameof(AppTheme.Dark) or nameof(ElementTheme.Dark) => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        private async Task ApplySavedThemeAsync()
        {
            try
            {
                using var scope = Services.CreateScope();
                var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
                var themeName = await settingsRepo.GetAsync(AppThemeSettingsKey).ConfigureAwait(true);
                var theme = ParseElementTheme(themeName);

                ApplyTheme(theme);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao aplicar o tema salvo na inicialização da janela principal.");
            }
        }

        private static async Task MigrateDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
    }
}
