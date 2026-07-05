using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace WingetEasy.App.ViewModels;

public partial class PackageItemViewModel : ObservableObject
{
    private readonly UpdatesViewModel _parent;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string AvailableVersion { get; }
    public string Source { get; }
    public string Category { get; }

    public Brush GetBadgeBackground(string category)
    {
        var resourceKey = category switch
        {
            "Segurança" => "#D92D2D",
            "Runtime" => "#D97706",
            _ => "#1A73E8"
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

    // Iniciais para gerar o ícone "VS", "Ch", etc (igual ao design)
    public string Initials => Name.Length >= 2 ? Name.Substring(0, 2).ToUpper() : Name.ToUpper();

    // Cores dinâmicas para o ícone com base no tamanho do nome (simulando a variedade do design)
    public string IconColorHex => (Name.Length % 3) == 0 ? "#4A8BFF" : (Name.Length % 2) == 0 ? "#10B981" : "#A855F7";

    public PackageItemViewModel(string id, string name, string version, string availableVersion, string source, string category, UpdatesViewModel parent)
    {
        Id = id;
        Name = name;
        Version = version;
        AvailableVersion = availableVersion;
        Source = source;
        Category = category;
        _parent = parent;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _parent.UpdateSelectionState();
    }

    [RelayCommand]
    private void CopyId()
    {
        var dp = new DataPackage();
        dp.SetText(Id);
        Clipboard.SetContent(dp);
    }

    [RelayCommand]
    private async Task ViewInWingetAsync()
    {
        // Abre o navegador padrão na página do pacote com o ConfigureAwait explícito
        await Launcher.LaunchUriAsync(new Uri($"https://winget.run/pkg/{Source}/{Id}")).AsTask().ConfigureAwait(true);
    }

    [RelayCommand]
    private void SkipOnce()
    {
        _parent.RemovePackageFromList(this);
    }

    [RelayCommand]
    private async Task SkipAlwaysAsync()
    {
        await _parent.SkipPackagePermanentlyAsync(this).ConfigureAwait(true);
    }
}
