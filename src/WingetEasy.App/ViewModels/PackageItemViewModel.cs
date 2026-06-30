using CommunityToolkit.Mvvm.ComponentModel;

namespace WingetEasy.App.ViewModels;

/// <summary>
/// Representa um pacote individual na lista de atualizações.
/// </summary>
public partial class PackageItemViewModel : ObservableObject
{
    private readonly UpdatesViewModel _parent;

    // Nova sintaxe AOT para WinUI 3 (Propriedade parcial em vez de campo privado)
    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    // Propriedades visuais do pacote
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string AvailableVersion { get; }

    public PackageItemViewModel(string id, string name, string version, string availableVersion, UpdatesViewModel parent)
    {
        Id = id;
        Name = name;
        Version = version;
        AvailableVersion = availableVersion;
        _parent = parent;
    }

    // Este método continua a ser gerado automaticamente e avisará o ViewModel pai
    partial void OnIsSelectedChanged(bool value)
    {
        _parent.UpdateSelectionState();
    }
}
