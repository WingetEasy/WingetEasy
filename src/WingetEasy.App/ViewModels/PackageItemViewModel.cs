using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using WingetEasy.Core.Models;

namespace WingetEasy.App.ViewModels;

public enum PackageUpdateState
{
    Pending,
    Queued,
    Updating,
    Completed,
    Failed
}

public partial class PackageItemViewModel : ObservableObject
{
    // Paleta determinística vibrante: Azul, Verde, Roxo, Laranja, Preto
    private static readonly string[] PaletteHex =
    [
        "#0A84FF", // Azul
        "#16A34A", // Verde
        "#8B5CF6", // Roxo
        "#F1502F", // Laranja
        "#111827"  // Preto
    ];

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SolidColorBrush> BrushCache = new();
    private readonly UpdatesViewModel _parent;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    [NotifyPropertyChangedFor(nameof(IsProgressVisibility))]
    [NotifyPropertyChangedFor(nameof(IsCompletedVisible))]
    [NotifyPropertyChangedFor(nameof(IsFailedVisible))]
    [NotifyPropertyChangedFor(nameof(IsActionButtonVisible))]
    public partial PackageUpdateState UpdateState { get; set; } = PackageUpdateState.Pending;

    [ObservableProperty]
    public partial int Progress { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public Visibility IsSelectionVisible => UpdateState == PackageUpdateState.Pending ? Visibility.Visible : Visibility.Collapsed;
    public bool IsProgressVisible => UpdateState is PackageUpdateState.Queued or PackageUpdateState.Updating;
    public Visibility IsProgressVisibility => IsProgressVisible ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsCompletedVisible => UpdateState == PackageUpdateState.Completed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsFailedVisible => UpdateState == PackageUpdateState.Failed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsActionButtonVisible => UpdateState is PackageUpdateState.Pending or PackageUpdateState.Failed
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string AvailableVersion { get; }
    public string Source { get; }
    public string Category { get; }
    public string Publisher { get; }
    public string PublisherDisplay { get; }

    public Visibility IsSecurityCategory => Category == "Segurança" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsRuntimeCategory => Category == "Runtime" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsAppCategory => Category != "Segurança" && Category != "Runtime" ? Visibility.Visible : Visibility.Collapsed;

    public string Initials { get; }
    public string IconColorHex { get; }
    public SolidColorBrush IconBrush { get; }

    public PackageItemViewModel(
        string id,
        string name,
        string version,
        string availableVersion,
        string source,
        string category,
        UpdatesViewModel parent)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        Version = version;
        AvailableVersion = availableVersion;
        Source = string.IsNullOrWhiteSpace(source) ? "winget" : source;
        Category = category;
        _parent = parent;

        Publisher = ExtractPublisher(id);
        PublisherDisplay = string.Equals(Publisher, Id, StringComparison.OrdinalIgnoreCase)
            ? Id
            : $"{Publisher} · {Id}";

        Initials = ComputeInitials(Name, Id);
        IconColorHex = ComputeColorHex(Name, Id);
        IconBrush = BrushCache.GetOrAdd(IconColorHex, static hex => new SolidColorBrush(ParseHexColor(hex)));
    }

    private static string ExtractPublisher(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "winget";

        var dotIndex = id.IndexOf('.');
        return dotIndex > 0 ? id[..dotIndex] : id;
    }

    private static string ComputeInitials(string name, string id)
    {
        var combined = $"{name} {id}";
        if (combined.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("VSCode", StringComparison.OrdinalIgnoreCase))
            return "VS";
        if (combined.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            return "Ch";
        if (combined.Contains("PowerToys", StringComparison.OrdinalIgnoreCase))
            return "PT";
        if (combined.Contains("Notion", StringComparison.OrdinalIgnoreCase))
            return "N";
        if (combined.Contains("Git", StringComparison.OrdinalIgnoreCase) && !combined.Contains("GitHub", StringComparison.OrdinalIgnoreCase))
            return "Gi";
        if (combined.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            return "Sp";
        if (combined.Contains("7-Zip", StringComparison.OrdinalIgnoreCase) || combined.Contains("7zip", StringComparison.OrdinalIgnoreCase))
            return "7z";

        var clean = name.Trim();
        if (string.IsNullOrEmpty(clean))
            return "PK";

        var words = clean.Split([' ', '-', '.', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetterOrDigit(w[0]))
            .ToArray();

        if (words.Length >= 2 && char.IsLetter(words[0][0]) && char.IsLetter(words[1][0]))
        {
            return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
        }

        if (clean.Length >= 2)
        {
            return $"{char.ToUpperInvariant(clean[0])}{char.ToLowerInvariant(clean[1])}";
        }

        return clean.ToUpperInvariant();
    }

    private static string ComputeColorHex(string name, string id)
    {
        var combined = $"{name} {id}";
        if (combined.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("VSCode", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Telegram", StringComparison.OrdinalIgnoreCase))
            return PaletteHex[0]; // Azul
        if (combined.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Node", StringComparison.OrdinalIgnoreCase))
            return PaletteHex[1]; // Verde
        if (combined.Contains("PowerToys", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Figma", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Discord", StringComparison.OrdinalIgnoreCase))
            return PaletteHex[2]; // Roxo
        if (combined.Contains("Git", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("VLC", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Postman", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Brave", StringComparison.OrdinalIgnoreCase))
            return PaletteHex[3]; // Laranja
        if (combined.Contains("Notion", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Terminal", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("OBS", StringComparison.OrdinalIgnoreCase))
            return PaletteHex[4]; // Preto

        int hash = 0;
        foreach (var ch in id)
        {
            hash = ((hash * 31) + char.ToLowerInvariant(ch)) & 0x7FFFFFFF;
        }

        return PaletteHex[hash % PaletteHex.Length];
    }

    private static Color ParseHexColor(string hex)
    {
        var s = hex.TrimStart('#');
        byte r = Convert.ToByte(s[..2], 16);
        byte g = Convert.ToByte(s[2..4], 16);
        byte b = Convert.ToByte(s[4..6], 16);
        return Color.FromArgb(255, r, g, b);
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

    public static string Classify(WingetPackage package)
    {
        if (package.IsSecurityUpdate)
        {
            return "Segurança";
        }

        var identity = $"{package.Id} {package.Name}";
        if (ContainsAny(identity, "Microsoft.DotNet", "VCRedist", "WindowsAppRuntime",
            "DirectX", "OpenJDK", "Runtime", "Redistributable", "SDK"))
        {
            return "Runtime";
        }

        if (ContainsAny(identity, "Chrome", "Firefox", "Edge", "Brave", "Opera",
            "Antivirus", "Defender", "Security", "Crypt", "KeePass", "Malware"))
        {
            return "Segurança";
        }

        return "App";
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    partial void OnIsSelectedChanged(bool value)
    {
        _parent.UpdateSelectionState();
    }

    [RelayCommand]
    private async Task UpdateSingleAsync()
    {
        await _parent.UpdateSinglePackageAsync(this).ConfigureAwait(true);
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
        await Launcher.LaunchUriAsync(new Uri($"https://winget.run/pkg/{Source}/{Id}")).AsTask().ConfigureAwait(true);
    }

    [RelayCommand]
    private void SkipOnce()
    {
        _parent.DeferPackage(this);
    }

    [RelayCommand]
    private async Task SkipAlwaysAsync()
    {
        await _parent.SkipPackagePermanentlyAsync(this).ConfigureAwait(true);
    }
}
