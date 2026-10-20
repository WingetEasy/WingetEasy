using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
            this.InitializeComponent();

            ViewModel.SettingsLoaded += (_, _) =>
            {
                UpdateFrequencyButtonsVisual(ViewModel.FrequencyIndex);
                UpdateThemeButtonsVisual(ViewModel.ThemeIndex);
            };

            this.Loaded += async (s, e) =>
            {
                await ViewModel.InitializeAsync().ConfigureAwait(true);
                UpdateFrequencyButtonsVisual(ViewModel.FrequencyIndex);
                UpdateThemeButtonsVisual(ViewModel.ThemeIndex);
            };
        }

        private void FrequencySegmentedButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int index))
            {
                ViewModel.FrequencyIndex = index;
                UpdateFrequencyButtonsVisual(index);
            }
        }

        private void ThemeSegmentedButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int index))
            {
                ViewModel.ThemeIndex = index;
                UpdateThemeButtonsVisual(index);
            }
        }

        private void UpdateFrequencyButtonsVisual(int selectedIndex)
        {
            SetSegmentedButtonState(FreqBtn0, selectedIndex == 0);
            SetSegmentedButtonState(FreqBtn1, selectedIndex == 1);
            SetSegmentedButtonState(FreqBtn2, selectedIndex == 2);
            SetSegmentedButtonState(FreqBtn3, selectedIndex == 3);
        }

        private void UpdateThemeButtonsVisual(int selectedIndex)
        {
            SetSegmentedButtonState(ThemeBtn0, selectedIndex == 0);
            SetSegmentedButtonState(ThemeBtn1, selectedIndex == 1);
            SetSegmentedButtonState(ThemeBtn2, selectedIndex == 2);
        }

        private static void SetSegmentedButtonState(Button button, bool isSelected)
        {
            button.Background = isSelected
                ? (Brush)Application.Current.Resources["SegmentedThumbBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Foreground = isSelected
                ? (Brush)Application.Current.Resources["BrandTextPrimaryBrush"]
                : (Brush)Application.Current.Resources["BrandTextSecondaryBrush"];
            button.FontWeight = isSelected
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;
        }
    }
}
