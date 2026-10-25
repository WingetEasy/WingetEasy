using WingetEasy.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WingetEasy.App.ViewModels;
using WingetEasy.App.Controls;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace WingetEasy.App.Pages;

public sealed partial class ProfilesPage : Page
{
    public ProfilesViewModel ViewModel { get; }

    public ProfilesPage()
    {
        ViewModel = App.Services.GetRequiredService<ProfilesViewModel>();
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        DataContext = ViewModel;
        LocalizationService.Instance.LanguageChanged += () => LocalizationService.Instance.LocalizeElement(this);
        Loaded += async (_, _) =>
        {
            LocalizationService.Instance.LocalizeElement(this);
            if (ViewModel.Profiles.Count == 0) await ViewModel.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        };

    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ProfileItemViewModel item } || item.Profile.Packages.Count == 0)
        {
            return;
        }

        var installedIds = (await ViewModel.GetInstalledAsync().ConfigureAwait(true))
            .Select(package => package.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var checklist = new PackageChecklist();
        foreach (var package in item.Profile.Packages)
        {
            var installed = installedIds.Contains(package.Id);
            checklist.AddPackage(package.Id, package.Name, !installed, installed ? "já instalado" : null);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Instalar \"{item.Name}\"",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Revise os pacotes. Os já instalados começam desmarcados.", TextWrapping = TextWrapping.Wrap },
                    checklist
                }
            }
        };

        void Refresh()
        {
            dialog.PrimaryButtonText = $"Instalar Selecionados ({checklist.SelectedCount})";
            dialog.IsPrimaryButtonEnabled = checklist.SelectedCount > 0;
        }

        checklist.SelectionChanged += Refresh;
        Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var ids = checklist.SelectedPackages.Select(package => package.Id).ToList();
        await ViewModel.InstallSelectedAsync(item, ids).ConfigureAwait(true);
    }

    private async void NewProfileButton_Click(object sender, RoutedEventArgs e) => await ShowEditorAsync(null).ConfigureAwait(true);

    private async void ImportProfileButton_Click(object sender, RoutedEventArgs e) => await ViewModel.ImportAsync().ConfigureAwait(true);

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileItemViewModel item })
        {
            await ShowEditorAsync(item).ConfigureAwait(true);
        }
    }

    private async void ExportScriptButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileItemViewModel item })
        {
            await ViewModel.ExportAsync(item, asScript: true).ConfigureAwait(true);
        }
    }

    private async void ExportJsonButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileItemViewModel item })
        {
            await ViewModel.ExportAsync(item, asScript: false).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Diálogo único para criar (existing == null) ou editar um perfil, com seleção de pacotes por checkbox.
    /// </summary>
    private async Task ShowEditorAsync(ProfileItemViewModel? existing)
    {
        var installed = await ViewModel.GetInstalledAsync().ConfigureAwait(true);
        var nameBox = new TextBox
        {
            Header = "Nome do Perfil",
            PlaceholderText = "Ex.: Setup Trabalho 2026",
            Text = existing?.Profile.Name ?? string.Empty
        };
        var descriptionBox = new TextBox
        {
            Header = "Descrição (opcional)",
            Text = existing?.Profile.Description ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 72
        };
        var checklist = new PackageChecklist();
        foreach (var package in existing?.Profile.Packages ?? [])
        {
            checklist.AddPackage(package.Id, package.Name, true);
        }

        var search = new AutoSuggestBox
        {
            PlaceholderText = "Buscar pacote instalado por nome ou ID",
            QueryIcon = new SymbolIcon(Symbol.Find)
        };
        search.TextChanged += (box, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var term = box.Text.Trim();
            box.ItemsSource = term.Length < 2
                ? null
                : installed
                    .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || p.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .Select(p => $"{p.Name} | {p.Id}")
                    .ToList();
        };
        var addAll = new Button { Content = $"Adicionar todos os instalados ({installed.Count})", IsEnabled = installed.Count > 0 };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = existing is null ? "Novo Perfil" : "Editar Perfil",
            PrimaryButtonText = "Salvar Perfil",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        nameBox,
                        descriptionBox,
                        new TextBlock { Text = "Pacotes do perfil", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        search,
                        addAll,
                        checklist
                    }
                }
            }
        };

        void Refresh() =>
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text) && checklist.SelectedCount > 0;

        search.SuggestionChosen += (_, args) =>
        {
            if (args.SelectedItem is string text)
            {
                var parts = text.Split(" | ", 2);
                checklist.AddPackage(parts[^1], parts[0], true);
                Refresh();
            }
        };
        search.QuerySubmitted += (box, _) => box.Text = string.Empty;
        addAll.Click += (_, _) =>
        {
            foreach (var package in installed)
            {
                if (!string.IsNullOrWhiteSpace(package.Id)) checklist.AddPackage(package.Id, package.Name, true);
            }

            Refresh();
        };
        nameBox.TextChanged += (_, _) => Refresh();
        checklist.SelectionChanged += Refresh;
        Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (existing is null)
        {
            await ViewModel.CreateProfileAsync(nameBox.Text, descriptionBox.Text, checklist.SelectedPackages).ConfigureAwait(true);
        }
        else
        {
            await ViewModel.UpdateProfileAsync(existing, nameBox.Text, descriptionBox.Text, checklist.SelectedPackages).ConfigureAwait(true);
        }
    }
    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileItemViewModel item })
        {
            await ViewModel.DeleteProfileCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }
}
