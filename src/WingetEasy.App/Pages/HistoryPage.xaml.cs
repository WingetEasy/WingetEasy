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
            if (sender is TextBox textBox && ViewModel is not null)
            {
                ViewModel.SearchText = textBox.Text;
            }
        }

        private void PeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0 && ViewModel is not null)
            {
                ViewModel.SelectedPeriod = (HistoryPeriod)comboBox.SelectedIndex;
            }
        }
    }
}
