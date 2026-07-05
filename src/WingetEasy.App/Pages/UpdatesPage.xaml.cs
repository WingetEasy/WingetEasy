using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
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
        }
    }
}
