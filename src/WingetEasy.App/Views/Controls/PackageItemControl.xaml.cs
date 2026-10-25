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

}
