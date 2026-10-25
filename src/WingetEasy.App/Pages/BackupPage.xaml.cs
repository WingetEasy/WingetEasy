using WingetEasy.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WingetEasy.App.ViewModels;
using WingetEasy.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WingetEasy.App.Pages;

public sealed partial class BackupPage : Page
{
    public BackupViewModel ViewModel { get; }

    public BackupPage()
    {
        ViewModel = App.Services.GetRequiredService<BackupViewModel>();
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        DataContext = ViewModel;
        ViewModel.InitializeFilePicker(App.MainWindowHandle);
        LocalizationService.Instance.LanguageChanged += () => LocalizationService.Instance.LocalizeElement(this);
        Loaded += async (_, _) =>
        {
            LocalizationService.Instance.LocalizeElement(this);
            if (ViewModel.Snapshots.Count == 0) await ViewModel.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        };

    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: BackupSnapshotItemViewModel item })
        {
            return;
        }

        var packages = await ViewModel.GetSnapshotPackagesAsync(item).ConfigureAwait(true);
        if (packages.Count == 0)
        {
            return;
        }

        var (rows, list) = BuildPackageChecklist(packages);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Restaurar \"{item.Label}\"",
            PrimaryButtonText = $"Restaurar Selecionados ({rows.Count})",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Desmarque os pacotes que não deseja reinstalar.", TextWrapping = TextWrapping.Wrap },
                    list
                }
            }
        };

        void Refresh()
        {
            var count = rows.Count(r => r.IsChecked == true);
            dialog.PrimaryButtonText = $"Restaurar Selecionados ({count})";
            dialog.IsPrimaryButtonEnabled = count > 0;
        }

        foreach (var row in rows)
        {
            row.Checked += (_, _) => Refresh();
            row.Unchecked += (_, _) => Refresh();
        }

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var ids = rows.Where(r => r.IsChecked == true).Select(r => ((BackupPackage)r.Tag).Id).ToList();
        await ViewModel.RestoreSelectedAsync(item, ids).ConfigureAwait(true);
    }

    private async void CreateProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: BackupSnapshotItemViewModel item })
        {
            return;
        }

        var packages = await ViewModel.GetSnapshotPackagesAsync(item).ConfigureAwait(true);
        if (packages.Count == 0)
        {
            return;
        }

        var nameBox = new TextBox { Header = "Nome do Perfil", PlaceholderText = "Ex.: Setup Trabalho 2026", Text = item.Label };
        var selectAll = new CheckBox { Content = "Marcar/Desmarcar todos", IsThreeState = true, IsChecked = true };
        var descriptionBox = new TextBox
        {
            Header = "Descrição (opcional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 72
        };
        var (rows, list) = BuildPackageChecklist(packages);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Criar Perfil a partir do Snapshot",
            PrimaryButtonText = "Salvar Perfil",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel { Spacing = 10, Children = { nameBox, descriptionBox, selectAll, list } }
        };

        void Refresh() =>
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text) && rows.Any(r => r.IsChecked == true);

        nameBox.TextChanged += (_, _) => Refresh();

        var syncingSelectAll = false;
        void SyncSelectAll()
        {
            syncingSelectAll = true;
            var checkedCount = rows.Count(r => r.IsChecked == true);
            selectAll.IsChecked = checkedCount == rows.Count ? true : checkedCount == 0 ? false : null;
            syncingSelectAll = false;
        }

        selectAll.Click += (_, _) =>
        {
            if (syncingSelectAll) return;
            // Estado indeterminado vira "marcar todos" ao clicar.
            var target = selectAll.IsChecked == true;
            foreach (var row in rows) row.IsChecked = target;
            SyncSelectAll();
            Refresh();
        };
        foreach (var row in rows)
        {
            row.Checked += (_, _) => SyncSelectAll();
            row.Unchecked += (_, _) => SyncSelectAll();
        }
        foreach (var row in rows)
        {
            row.Checked += (_, _) => Refresh();
            row.Unchecked += (_, _) => Refresh();
        }
        Refresh();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var selected = rows.Where(r => r.IsChecked == true).Select(r => (BackupPackage)r.Tag).ToList();
        await ViewModel.SaveAsProfileAsync(nameBox.Text, descriptionBox.Text, selected).ConfigureAwait(true);
    }

    private static (List<CheckBox> Rows, ScrollViewer List) BuildPackageChecklist(IReadOnlyList<BackupPackage> packages)
    {
        var panel = new StackPanel { Spacing = 2 };
        var rows = new List<CheckBox>(packages.Count);
        foreach (var package in packages.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var label = string.IsNullOrWhiteSpace(package.Name) ? package.Id : $"{package.Name} ({package.Id})";
            var box = new CheckBox { Content = label, IsChecked = true, Tag = package };
            rows.Add(box);
            panel.Children.Add(box);
        }

        return (rows, new ScrollViewer { MaxHeight = 320, Content = panel });
    }
    private async void ExportScriptButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotItemViewModel item })
        {
            await ViewModel.ExportScriptCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private async void ExportJsonButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotItemViewModel item })
        {
            await ViewModel.ExportJsonCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotItemViewModel item })
        {
            await ViewModel.DeleteSnapshotCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }
}
