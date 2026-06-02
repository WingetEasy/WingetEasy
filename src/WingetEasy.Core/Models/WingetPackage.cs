namespace WingetEasy.Core.Models;

public record WingetPackage (
    string Id,
    string Name,
    string CurrentVersion,
    string AvailableVersion,
    string? Source,
    PackageCategory Category = PackageCategory.App,
    bool IsSecurityUpdate = false
);
