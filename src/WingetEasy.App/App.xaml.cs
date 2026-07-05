using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using WingetEasy.App.Services;
using WingetEasy.App.ViewModels;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace WingetEasy.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private H.NotifyIcon.TaskbarIcon? _trayIcon;

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

            if (args.Contains("--elevated"))
            {
                Log.Information("Iniciando processo filho com privilégios de Administrador (UAC).");
                RunElevatedModeAndExit(args);
            }

            InitializeComponent();

            Services = ConfigureServices();
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
                        var job = JsonSerializer.Deserialize<Core.Models.ElevatedJob>(json);

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
                                        Arguments  = $"update --exact --id {id} --accept-package-agreements --accept-source-agreements --silent",
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true
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

                            var resultFile = jobFile.Replace("job", "result");
                            File.WriteAllText(resultFile, JsonSerializer.Serialize(results));
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

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(dispose: true);
            });

            services.AddDbContext<AppDbContext>();

            // Repositórios injetados com escopo AddScoped
            services.AddScoped<ISettingsRepository, WingetEasy.Data.Repositories.SettingsRepository>();
            services.AddScoped<IUpdateHistoryRepository, WingetEasy.Data.Repositories.UpdateHistoryRepository>();
            services.AddScoped<IPackageRepository, WingetEasy.Data.Repositories.PackageRepository>();
            services.AddScoped<ICheckLogRepository, WingetEasy.Data.Repositories.CheckLogRepository>();

            // Alinhamento das regras de arquitetura: Serviços principais como Singletons
            services.AddSingleton<IWingetService, WingetEasy.Core.Services.WingetService>();
            services.AddSingleton<IUpdateService, WingetEasy.Core.Services.UpdateService>();
            services.AddSingleton<ISchedulerService, WingetEasy.Core.Services.SchedulerService>();
            services.AddSingleton<INotificationService, ToastNotificationService>();
            services.AddSingleton<IAdminElevationHelper, WingetEasy.Core.Services.AdminElevationHelper>();

            // Lógica de Apresentação resolvida como Transient para evitar vazamento de estado
            services.AddTransient<UpdatesViewModel>();
            services.AddTransient<SettingsViewModel>();

            return services.BuildServiceProvider();
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // 1. Aplica as migrations do banco antes de inicializar qualquer lógica
                await MigrateDatabaseAsync(Services).ConfigureAwait(true);

                // 2. Acorda o SchedulerService assim que o banco está estável
                var scheduler = Services.GetRequiredService<ISchedulerService>();
                await scheduler.StartAsync().ConfigureAwait(true);

                // 3. Monta e expõe o ícone na System Tray (Bandeja do Sistema)
                InitializeTrayIcon();

                // 4. Analisa os argumentos recebidos do sistema operacional
                var commandArgs = Environment.GetCommandLineArgs();
                bool minimized = commandArgs.Contains("--minimized");

                if (!minimized)
                {
                    Log.Information("Inicialização padrão detectada. Aguardando interação via System Tray.");
                }
                else
                {
                    Log.Information("WingetEasy carregado silenciosamente via parâmetro --minimized.");
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ocorreu um erro fatal durante o provisionamento de infraestrutura da UI.");
                throw;
            }
        }

        private void InitializeTrayIcon()
        {
            // 1. CRIANDO OS ITENS USANDO "COMMANDS" (À prova de falhas na Bandeja do Sistema)
            var menuVerificar = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Verificar agora", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE72C" } };
            menuVerificar.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => {
                Log.Information("Usuário clicou em Verificar Agora.");
            });

            var menuAtualizacoes = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Atualizações disponíveis (0)", Visibility = Microsoft.UI.Xaml.Visibility.Collapsed, Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE895" } };
            menuAtualizacoes.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow);

            var menuConfig = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Configurações", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE713" } };
            menuConfig.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow);

            var menuHistorico = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Histórico", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE81C" } };
            menuHistorico.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow);

            var menuSobre = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Sobre", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE946" } };
            menuSobre.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow);

            var menuSair = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Sair", Icon = new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\xE7E8" } };
            menuSair.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => {
                Log.Information("Usuário solicitou encerramento via System Tray. Finalizando...");
                Log.CloseAndFlush(); // Força a gravação do log antes de matar o processo
                _trayIcon?.Dispose();
                Environment.Exit(0); // Marreta do sistema operativo: encerra TUDO na hora!
            });

            // 2. MONTANDO O FLYOUT (A CAIXINHA DO MENU)
            var contextMenu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
            contextMenu.Items.Add(menuVerificar);
            contextMenu.Items.Add(menuAtualizacoes);
            contextMenu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
            contextMenu.Items.Add(menuConfig);
            contextMenu.Items.Add(menuHistorico);
            contextMenu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
            contextMenu.Items.Add(menuSobre);
            contextMenu.Items.Add(menuSair);

            // 3. CONFIGURANDO A IMAGEM COLORIDA COM CAMINHO ABSOLUTO
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "WingetEasy-Logo.ico");

            // 4. INSTANCIANDO O TRAY ICON
            _trayIcon = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = "WingetEasy",
                IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath)),
                ContextFlyout = contextMenu,
                LeftClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow)
            };

            // Manda para a bandeja do Windows
            _trayIcon.ForceCreate();
        }

        /// <summary>
        /// Atualiza o estado visual da bandeja do sistema (Ícone e Menu) com base no número de atualizações.
        /// </summary>
        public void UpdateTrayState(int updateCount)
        {
            if (_trayIcon == null) return;

            bool hasUpdates = updateCount > 0;

            // 1. Alterna entre o ficheiro normal e o ficheiro com o Badge
            // NOTA: Certifique-se de que o nome do ficheiro com o badge bate certo com o que colocar na pasta Assets
            var fileName = hasUpdates ? "WingetEasy-Logo-update.ico" : "WingetEasy-Logo.ico";
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", fileName);

            _trayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));

            // 2. Atualiza o botão no Menu Contextual
            if (_trayIcon.ContextFlyout is Microsoft.UI.Xaml.Controls.MenuFlyout menuFlyout)
            {
                // No nosso código, o item de atualizações é o segundo elemento (índice 1)
                if (menuFlyout.Items[1] is Microsoft.UI.Xaml.Controls.MenuFlyoutItem menuAtualizacoes)
                {
                    menuAtualizacoes.Visibility = hasUpdates ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
                    menuAtualizacoes.Text = $"Atualizações disponíveis ({updateCount})";
                }
            }
        }

        private void ShowMainWindow()
        {
            if (_window == null)
            {
                _window = new MainWindow();
                // Limpa a instância ao fechar para permitir a recriação limpa posterior
                _window.Closed += (s, e) => _window = null;

                _ = ApplySavedThemeAsync();
            }
            _window.Activate();
        }

        /// <summary>
        /// Aplica de imediato o tema (Claro/Escuro/Sistema) ao elemento raiz da janela informado.
        /// Chamado tanto na abertura da janela quanto em tempo real pela SettingsPage.
        /// </summary>
        public void ApplyTheme(ElementTheme theme)
        {
            if (_window?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme;
            }
        }

        private async Task ApplySavedThemeAsync()
        {
            try
            {
                using var scope = Services.CreateScope();
                var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
                var themeName = await settingsRepo.GetAsync(AppThemeSettingsKey).ConfigureAwait(true);
                var theme = Enum.TryParse<ElementTheme>(themeName, out var parsed) ? parsed : ElementTheme.Default;

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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
    }
}
