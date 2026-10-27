using WingetEasy.App.Services;
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
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            System.EventHandler onSettingsLoaded = (_, _) =>
            {
                UpdateFrequencyButtonsVisual(ViewModel.FrequencyIndex);
                UpdateThemeButtonsVisual(ViewModel.ThemeIndex);
            };

            ViewModel.SettingsLoaded += onSettingsLoaded;
            LocalizationService.Instance.LanguageChanged += () =>
            {
                LocalizationService.Instance.LocalizeElement(this);
                UpdateFrequencyButtonsVisual(ViewModel.FrequencyIndex);
                UpdateThemeButtonsVisual(ViewModel.ThemeIndex);
            };

            this.Loaded += async (s, e) =>
            {
                LocalizationService.Instance.LocalizeElement(this);
                await ViewModel.InitializeAsync().ConfigureAwait(true);
                UpdateFrequencyButtonsVisual(ViewModel.FrequencyIndex);
                UpdateThemeButtonsVisual(ViewModel.ThemeIndex);
            };

            this.ActualThemeChanged += (_, _) =>
            {
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

        private Brush GetThemeBrush(string key)
        {
            var effectiveTheme = this.ActualTheme;
            if (effectiveTheme == ElementTheme.Default)
            {
                effectiveTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark
                    ? ElementTheme.Dark
                    : ElementTheme.Light;
            }

            var themeKey = effectiveTheme == ElementTheme.Light ? "Light" : "Dark";

            Brush? FindIn(ResourceDictionary dict)
            {
                if (dict.ThemeDictionaries.TryGetValue(themeKey, out var themeObj) &&
                    themeObj is ResourceDictionary themeDict &&
                    themeDict.TryGetValue(key, out var b) && b is Brush brush)
                {
                    return brush;
                }

                foreach (var merged in dict.MergedDictionaries)
                {
                    var found = FindIn(merged);
                    if (found != null) return found;
                }

                return null;
            }

            var result = FindIn(Application.Current.Resources);
            if (result != null) return result;

            if (Application.Current.Resources.TryGetValue(key, out var fallbackObj) && fallbackObj is Brush fallbackBrush)
            {
                return fallbackBrush;
            }

            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        private void SetSegmentedButtonState(Button button, bool isSelected)
        {
            button.Background = isSelected
                ? GetThemeBrush("SegmentedThumbBrush")
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Foreground = isSelected
                ? GetThemeBrush("BrandTextPrimaryBrush")
                : GetThemeBrush("BrandTextSecondaryBrush");
            button.FontWeight = isSelected
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;
        }
    }
}
