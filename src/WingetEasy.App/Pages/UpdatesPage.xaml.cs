using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Pages
{
    public sealed partial class UpdatesPage : Page
    {
        public UpdatesViewModel ViewModel { get; }

        public UpdatesPage()
        {
            ViewModel = App.Services.GetRequiredService<UpdatesViewModel>();
            this.InitializeComponent(); // OBRIGATÓRIO PARA O XAML COMPILAR
            ViewModel.ElevationConfirmation = ConfirmElevationAsync;
            Unloaded += (_, _) =>
            {
                if (ViewModel.ElevationConfirmation == (Func<Task<bool>>)ConfirmElevationAsync)
                {
                    ViewModel.ElevationConfirmation = null;
                }
            };
        }

        private async Task<bool> ConfirmElevationAsync()
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Permissão de administrador necessária",
                Content = "Alguns pacotes exigem privilégios de Administrador para serem atualizados.",
                PrimaryButtonText = "Continuar com Administrador",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
    }
}
