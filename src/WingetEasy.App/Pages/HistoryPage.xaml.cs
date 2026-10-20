using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Pages
{
    public sealed partial class HistoryPage : Page
    {
        public HistoryViewModel ViewModel { get; }

        public HistoryPage()
        {
            ViewModel = App.Services.GetRequiredService<HistoryViewModel>();
            InitializeComponent();
            ViewModel.InitializeFilePicker(App.MainWindowHandle);
            Loaded += async (_, _) => await ViewModel.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchText = SearchTextBox.Text;
        }

        private void PeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeriodComboBox.SelectedIndex >= 0)
            {
                ViewModel.SelectedPeriod = (HistoryPeriod)PeriodComboBox.SelectedIndex;
            }
        }
    }
}
