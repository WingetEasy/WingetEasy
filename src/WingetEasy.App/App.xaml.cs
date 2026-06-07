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

using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WingetEasy.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>

    public partial class App : Application
    {
        private Window? _window;

        // 1. Propriedade pública para fazer à Injeção de Dependência em toda a app
        public IServiceProvider Services { get; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>

        public App()
        {
            // INTERCEpTA A EXECUÇÃO LOGO NO INÍCIO SE FOR O PROCESSO FILHO ELEVADO
            var args = Environment.GetCommandLineArgs();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logPath = System.IO.Path.Combine(appData, "WingetEasy", "Logs", "app-.log");
            var isDev = args.Contains("--dev");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(isDev ? LogEventLevel.Debug : LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning) // Silencia o EF Core
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true, // permite que o app principal e o processo UAC escrevam juntos no log
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("==== Inicializando WingetEasy ====");

            // INTERCEpTA A EXECUÇÃO LOGO NO INÍCIO SE FOR O PROCESSO FILHO ELEVADO
            if (args.Contains("--elevated"))
            {
                Log.Information("Iniciando processo filho com privilégios de Administrador (UAC)."); // ---> AQUI: Log de inicialização do UAC
                RunElevatedModeAndExit(args);
            }


            InitializeComponent();

            // Inicialia os serviços assim que inicia
            Services = ConfigureServices();
        }

        /// <summary>
        /// Executa as atualizações de forma invisível e encerra.
        /// Este método SÓ é chamado pelo processo filho rodando como Administrador.
        /// </summary>

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

                                // O processo já está elevado (passou pelo UAC), então o winget não pedirá permissão de novo
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

                                // Log do resultado individual de cada pacote dentro do UAC
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
                Log.Fatal(ex, "Erro fatal não tratado no processo filho elevado."); // Log de erro fatal no UAC
            }
            finally
            {
                // Garante que o Serilog escreve tudo no disco antes de matar o processo filho
                Log.CloseAndFlush();

                // NUNCA FICA ÓRFÃO. FECHA O PROCESSO FILHO IMEDIATAMENTE.
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Configura o contêiner de Injeção de Dependência (DI).
        /// </summary>

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(dispose: true);
            });

            // Registra o DBCONTEXT
            services.AddDbContext<AppDbContext>();

            // Aqui você pode registrar outros serviços, repositórios, etc.
            services.AddScoped<ISettingsRepository, WingetEasy.Data.Repositories.SettingsRepository>();
            services.AddScoped<IUpdateHistoryRepository, WingetEasy.Data.Repositories.UpdateHistoryRepository>();
            services.AddScoped<IPackageRepository, WingetEasy.Data.Repositories.PackageRepository>();
            services.AddScoped<ICheckLogRepository, WingetEasy.Data.Repositories.CheckLogRepository>();

            services.AddScoped<IAdminElevationHelper, WingetEasy.Core.Services.AdminElevationHelper>();
            services.AddSingleton<ISchedulerService, WingetEasy.Core.Services.SchedulerService>();

            services.AddScoped<IWingetService, WingetEasy.Core.Services.WingetService>();
            services.AddScoped<IUpdateService, WingetEasy.Core.Services.UpdateService>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // executa a migração da bse de dados antes de mostrar a janela principal
                await MigrateDatabaseAsync(Services).ConfigureAwait(true);

                _window = new MainWindow();
                _window.Activate();

                Log.Information("WingetEasy iniciado com sucesso."); // Log de sucesso
            }

            catch (Exception ex)
            {
                Log.Fatal(ex, "Ocorreu um erro fatal durante a inicialização da interface principal."); // Log caso a app não consiga abrir
                throw;
            }
        }

        /// <summary>
        /// Executa as migrações pendentes da base de dados local no momento da inicialização.
        /// Cria o ficheiro data.db e as tabelas caso não existam.
        /// </summary>

        private async Task MigrateDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // O EF Core vai criar a base e aplicar o esquema atual
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }
    }

}
