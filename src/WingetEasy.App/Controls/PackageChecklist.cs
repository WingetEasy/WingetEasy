using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WingetEasy.Core.Models;

namespace WingetEasy.App.Controls;

/// <summary>
/// Lista de pacotes com checkboxes e a opção "Marcar/Desmarcar todos", reutilizável em diálogos.
/// </summary>
public sealed class PackageChecklist : StackPanel
{
    private readonly CheckBox _selectAll;
    private readonly StackPanel _rows = new() { Spacing = 2 };
    private readonly List<(CheckBox Box, ProfilePackage Package)> _items = [];
    private bool _syncing;

    public event Action? SelectionChanged;

    public int Count => _items.Count;
    public int SelectedCount => _items.Count(item => item.Box.IsChecked == true);
    public IReadOnlyList<ProfilePackage> SelectedPackages =>
        _items.Where(item => item.Box.IsChecked == true).Select(item => item.Package).ToList();

    public PackageChecklist()
    {
        Spacing = 6;
        _selectAll = new CheckBox { Content = "Marcar/Desmarcar todos", IsThreeState = true, IsChecked = true };
        _selectAll.Click += (_, _) =>
        {
            // O estado indeterminado vira "marcar todos" ao clicar.
            var target = _selectAll.IsChecked == true;
            _syncing = true;
            foreach (var item in _items) item.Box.IsChecked = target;
            _syncing = false;
            SyncSelectAll();
            SelectionChanged?.Invoke();
        };
        Children.Add(_selectAll);
        Children.Add(new ScrollViewer { MaxHeight = 280, Content = _rows });
    }

    /// <summary>
    /// Adiciona um pacote à lista. Retorna false se o ID já existir.
    /// </summary>
    public bool AddPackage(string id, string name, bool isChecked, string? note = null)
    {
        if (_items.Any(item => string.Equals(item.Package.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var displayName = string.IsNullOrWhiteSpace(name) ? id : name;
        var label = $"{displayName} ({id})" + (string.IsNullOrWhiteSpace(note) ? string.Empty : $" — {note}");
        var box = new CheckBox { Content = label, IsChecked = isChecked };
        box.Checked += (_, _) => OnRowChanged();
        box.Unchecked += (_, _) => OnRowChanged();
        _items.Add((box, new ProfilePackage(id, displayName)));
        _rows.Children.Add(box);
        SyncSelectAll();
        return true;
    }

    private void OnRowChanged()
    {
        if (_syncing) return;
        SyncSelectAll();
        SelectionChanged?.Invoke();
    }

    private void SyncSelectAll()
    {
        _syncing = true;
        var selected = SelectedCount;
        _selectAll.IsChecked = selected == _items.Count && selected > 0 ? true : selected == 0 ? false : null;
        _syncing = false;
    }
}

