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
            // Descobre o caminho físico real onde o app foi compilado e junta com a pasta Assets
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "WingetEasy-Logo.ico");

            _trayIcon = new H.NotifyIcon.TaskbarIcon
            {
                // caminho absoluto direto para a imagem!
                IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath)),
                ToolTipText = "WingetEasy",
                LeftClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow)
            };

            _trayIcon.ForceCreate();
        }

        private void ShowMainWindow()
        {
            if (_window == null)
            {
                _window = new MainWindow();
                // Limpa a instância ao fechar para permitir a recriação limpa posterior
                _window.Closed += (s, e) => _window = null;
            }
            _window.Activate();
        }

        private static async Task MigrateDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
    }
}
