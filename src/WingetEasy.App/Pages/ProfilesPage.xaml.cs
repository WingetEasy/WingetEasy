using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Pages;

public sealed partial class ProfilesPage : Page
{
    public ProfilesViewModel ViewModel { get; }

    public ProfilesPage()
    {
        ViewModel = App.Services.GetRequiredService<ProfilesViewModel>();
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        DataContext = ViewModel;
        Loaded += async (_, _) => { if (ViewModel.Profiles.Count == 0) await ViewModel.LoadCommand.ExecuteAsync(null).ConfigureAwait(true); };

    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileItemViewModel item })
        {
            await ViewModel.InstallProfileCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileItemViewModel item })
        {
            ViewModel.EditProfileCommand.Execute(item);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileItemViewModel item })
        {
            await ViewModel.DeleteProfileCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }
}
