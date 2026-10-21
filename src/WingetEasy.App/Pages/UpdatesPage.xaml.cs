using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using WingetEasy.App.ViewModels;

namespace WingetEasy.App.Pages
{
    public sealed partial class UpdatesPage : Page
    {
        public UpdatesViewModel ViewModel { get; }

        public UpdatesPage()
        {
            ViewModel = App.Services.GetRequiredService<UpdatesViewModel>();
            this.InitializeComponent();
            ViewModel.ElevationConfirmation = ConfirmElevationAsync;

            Loaded += (_, _) =>
            {
                ApplyHeroLogoShadow();
                if (!ViewModel.HasCheckedOnce && !ViewModel.IsChecking)
                {
                    ViewModel.CheckUpdatesCommand.Execute(null);
                }
            };

            Unloaded += (_, _) =>
            {
                if (ViewModel.ElevationConfirmation == (Func<Task<bool>>)ConfirmElevationAsync)
                {
                    ViewModel.ElevationConfirmation = null;
                }
            };
        }

        private void HeroLogoImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            ApplyHeroLogoShadow();
        }

        private void HeroLogoImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyHeroLogoShadow();
        }

        private void ApplyHeroLogoShadow()
        {
            if (HeroLogoImage is null || HeroLogoShadowHost is null ||
                HeroLogoImage.ActualWidth <= 0 || HeroLogoImage.ActualHeight <= 0)
            {
                return;
            }

            var hostVisual = ElementCompositionPreview.GetElementVisual(HeroLogoShadowHost);
            var compositor = hostVisual.Compositor;

            var dropShadow = compositor.CreateDropShadow();
            dropShadow.Color = Windows.UI.Color.FromArgb(60, 0, 0, 0);
            dropShadow.BlurRadius = 32f;
            dropShadow.Offset = new Vector3(0f, 6f, 0f);
            dropShadow.Mask = HeroLogoImage.GetAlphaMask();

            var shadowVisual = compositor.CreateSpriteVisual();
            shadowVisual.Size = new Vector2((float)HeroLogoImage.ActualWidth, (float)HeroLogoImage.ActualHeight);
            shadowVisual.Shadow = dropShadow;

            ElementCompositionPreview.SetElementChildVisual(HeroLogoShadowHost, shadowVisual);
        }

        private void CategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.Tag is string category)
            {
                if (button.Parent is StackPanel panel)
                {
                    foreach (var child in panel.Children.OfType<ToggleButton>())
                    {
                        child.IsChecked = ReferenceEquals(child, button);
                    }
                }
                ViewModel.CategoryFilter = category;
            }
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0)
            {
                ViewModel.SortBy = comboBox.SelectedIndex switch
                {
                    1 => "Categoria",
                    2 => "Fonte",
                    _ => "Nome"
                };
            }
        }

        private void SearchAccelerator_Invoked(
            Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
            Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectAll();
            args.Handled = true;
        }

        private async Task<bool> ConfirmElevationAsync()
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Permissão de administrador necessária",
                Content = "Alguns pacotes exigem privilégios de Administrador para serem atualizados.",
                PrimaryButtonText = "Continuar com Administrador",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
    }
}
