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
using WingetEasy.Core.Models;

namespace WingetEasy.App.Pages
{
    public sealed partial class UpdatesPage : Page
    {
        public UpdatesViewModel ViewModel { get; }

        public UpdatesPage()
        {
            ViewModel = App.Services.GetRequiredService<UpdatesViewModel>();
            this.InitializeComponent();
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel.ElevationConfirmation = ConfirmElevationAsync;
            ViewModel.AppRunningPrompt = ConfirmAppRunningResolutionAsync;

            Loaded += (_, _) =>
            {
                ViewModel.ElevationConfirmation = ConfirmElevationAsync;
                ViewModel.AppRunningPrompt = ConfirmAppRunningResolutionAsync;
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
                if (ViewModel.AppRunningPrompt == (Func<string, Task<AppRunningResolution>>)ConfirmAppRunningResolutionAsync)
                {
                    ViewModel.AppRunningPrompt = null;
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

            if (ElementCompositionPreview.GetElementChildVisual(HeroLogoShadowHost) is not null) return;
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

        private Task<bool> ConfirmElevationAsync()
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                return ShowElevationDialogCoreAsync();
            }

            var tcs = new TaskCompletionSource<bool>();
            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    tcs.TrySetResult(await ShowElevationDialogCoreAsync().ConfigureAwait(true));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        private async Task<bool> ShowElevationDialogCoreAsync()
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

        private Task<AppRunningResolution> ConfirmAppRunningResolutionAsync(string appName)
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                return ShowAppRunningDialogCoreAsync(appName);
            }

            var tcs = new TaskCompletionSource<AppRunningResolution>();
            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    tcs.TrySetResult(await ShowAppRunningDialogCoreAsync(appName).ConfigureAwait(true));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        private async Task<AppRunningResolution> ShowAppRunningDialogCoreAsync(string appName)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content?.XamlRoot ?? this.XamlRoot,
                Title = "Aplicativo em execução",
                Content = $"O aplicativo '{appName}' está aberto no momento.\n\nDeseja pular esta atualização por enquanto ou fechar o aplicativo e atualizar?",
                PrimaryButtonText = "Fechar e atualizar",
                CloseButtonText = "Pular por enquanto",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary
                ? AppRunningResolution.CloseAndUpdate
                : AppRunningResolution.Skip;
        }
    }
}
