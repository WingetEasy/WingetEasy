using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WingetEasy.App.Services;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Views.Controls;

public sealed partial class PackageItemControl : UserControl
{
    public PackageItemViewModel? ViewModel => DataContext as PackageItemViewModel;

    public PackageItemControl()
    {
        this.InitializeComponent();

        PackageMenuFlyout.Opening += (s, e) => LocalizationService.Instance.LocalizeFlyout(PackageMenuFlyout);

        LocalizationService.Instance.LanguageChanged += () =>
        {
            LocalizationService.Instance.LocalizeElement(this);
            LocalizationService.Instance.LocalizeFlyout(PackageMenuFlyout);
        };

        this.DataContextChanged += (s, e) =>
        {
            Bindings.Update();
            LocalizationService.Instance.LocalizeElement(this);
            LocalizationService.Instance.LocalizeFlyout(PackageMenuFlyout);
        };

        this.Loaded += (s, e) =>
        {
            LocalizationService.Instance.LocalizeElement(this);
            LocalizationService.Instance.LocalizeFlyout(PackageMenuFlyout);
        };
    }
}
