using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Views.Controls;

public sealed partial class PackageItemControl : UserControl
{
    public PackageItemViewModel? ViewModel => DataContext as PackageItemViewModel;

    public PackageItemControl()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => Bindings.Update();
    }

    public Brush GetBadgeBackground(string category)
    {
        var resourceKey = category switch
        {
            "Segurança" => "BadgeSecurityBgBrush",
            "Runtime" => "BadgeRuntimeBgBrush",
            _ => "BadgeAppBgBrush"
        };
        return (Brush)App.Current.Resources[resourceKey];
    }

    public Brush GetBadgeForeground(string category)
    {
        var resourceKey = category switch
        {
            "Segurança" => "BadgeSecurityFgBrush",
            "Runtime" => "BadgeRuntimeFgBrush",
            _ => "BadgeAppFgBrush"
        };
        return (Brush)App.Current.Resources[resourceKey];
    }
}
