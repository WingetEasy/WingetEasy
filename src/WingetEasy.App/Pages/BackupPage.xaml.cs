using WingetEasy.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Pages;

public sealed partial class BackupPage : Page
{
    public BackupViewModel ViewModel { get; }

    public BackupPage()
    {
        ViewModel = App.Services.GetRequiredService<BackupViewModel>();
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        DataContext = ViewModel;
        ViewModel.InitializeFilePicker(App.MainWindowHandle);
        LocalizationService.Instance.LanguageChanged += () => LocalizationService.Instance.LocalizeElement(this);
        Loaded += async (_, _) =>
        {
            LocalizationService.Instance.LocalizeElement(this);
            if (ViewModel.Snapshots.Count == 0) await ViewModel.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        };

    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotItemViewModel item })
        {
            await ViewModel.RestoreSnapshotCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private async void ExportScriptButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotItemViewModel item })
        {
            await ViewModel.ExportScriptCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private async void ExportJsonButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotItemViewModel item })
        {
            await ViewModel.ExportJsonCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotItemViewModel item })
        {
            await ViewModel.DeleteSnapshotCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }
}
