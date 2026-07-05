using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
            this.InitializeComponent(); // OBRIGATÓRIO PARA O XAML COMPILAR

            this.Loaded += async (s, e) => await ViewModel.InitializeAsync().ConfigureAwait(true);
        }
    }
}
