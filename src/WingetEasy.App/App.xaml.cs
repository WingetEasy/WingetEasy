using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
            InitializeComponent();

            // Inicialia os serviços assim que inicia
            Services = ConfigureServices();
        }

        /// <summary>
        /// Configura o contêiner de Injeção de Dependência (DI).
        /// </summary>

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Registra o DBCONTEXT
            services.AddDbContext<AppDbContext>();

            // Aqui você pode registrar outros serviços, repositórios, etc.

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // executa a migração da bse de dados antes de mostrar a janela principal
            await MigrateDatabaseAsync(Services).ConfigureAwait(true);

            _window = new MainWindow();
            _window.Activate();
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
