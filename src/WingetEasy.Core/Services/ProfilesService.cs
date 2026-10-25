using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

/// <summary>
/// Implementa o CRUD e a instalação de perfis de pacotes.
/// </summary>
public sealed class ProfilesService : IProfilesService
{
    private readonly IProfileRepository _repository;
    private readonly IWingetService _wingetService;
    private readonly IUpdateHistoryRepository _historyRepository;

    public ProfilesService(
        IProfileRepository repository,
        IWingetService wingetService,
        IUpdateHistoryRepository historyRepository)
    {
        _repository = repository;
        _wingetService = wingetService;
        _historyRepository = historyRepository;
    }

    public async Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken ct = default)
    {
        var profiles = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        if (profiles.Count > 0)
        {
            return profiles;
        }

        var seeded = new[] { CreateDevelopmentProfile(), CreateUtilitiesProfile() };
        foreach (var profile in seeded)
        {
            await _repository.SaveAsync(profile, ct).ConfigureAwait(false);
        }

        return seeded;
    }

    public Task<Profile> SaveAsync(Profile profile, CancellationToken ct = default)
    {
        if (profile.Id == Guid.Empty)
        {
            profile = profile with { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        }
        else if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("O nome do perfil é obrigatório.", nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("O nome do perfil é obrigatório.", nameof(profile));
        }

        return SaveProfileAsync(profile, ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repository.DeleteAsync(id, ct);

    public async Task ExportScriptAsync(Guid profileId, string path, CancellationToken ct = default)
    {
        var profile = await FindAsync(profileId, ct).ConfigureAwait(false);
        var builder = new StringBuilder();
        builder.AppendLine("# WingetEasy profile install script");
        builder.AppendLine($"# Profile: {SingleLine(profile.Name)}");
        if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            builder.AppendLine($"# Description: {SingleLine(profile.Description)}");
        }

        builder.AppendLine();
        foreach (var package in profile.Packages)
        {
            builder.AppendLine($"winget install --id \"{package.Id.Replace("\"", "`\"", StringComparison.Ordinal)}\" --exact --accept-source-agreements --accept-package-agreements");
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
    }

    public async Task ExportJsonAsync(Guid profileId, string path, CancellationToken ct = default)
    {
        var profile = await FindAsync(profileId, ct).ConfigureAwait(false);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("name", profile.Name);
            writer.WriteString("description", profile.Description);
            writer.WriteString("createdAt", profile.CreatedAt);
            writer.WriteStartArray("packages");
            foreach (var package in profile.Packages)
            {
                writer.WriteStartObject();
                writer.WriteString("id", package.Id);
                writer.WriteString("name", package.Name);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        await File.WriteAllBytesAsync(path, stream.ToArray(), ct).ConfigureAwait(false);
    }

    public async Task<Profile> ImportJsonAsync(string path, CancellationToken ct = default)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new InvalidDataException($"Não foi possível ler o arquivo: {ex.Message}", ex);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("O arquivo não é um JSON válido.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetProperty(root, out var packagesElement, "packages")
                || packagesElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("O arquivo não é compatível com o WingetEasy (lista de pacotes não encontrada).");
            }

            var packages = new List<ProfilePackage>();
            foreach (var element in packagesElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !TryGetProperty(element, out var idElement, "id")
                    || idElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("O arquivo contém um pacote sem identificador válido.");
                }

                var id = idElement.GetString()!.Trim();
                try
                {
                    WingetService.ValidatePackageId(id);
                }
                catch (ArgumentException)
                {
                    throw new InvalidDataException($"O arquivo contém um ID de pacote inválido: {id}");
                }

                var name = TryGetProperty(element, out var nameElement, "name") && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : null;
                packages.Add(new ProfilePackage(id, string.IsNullOrWhiteSpace(name) ? id : name.Trim()));
            }

            packages = packages.DistinctBy(package => package.Id, StringComparer.OrdinalIgnoreCase).ToList();
            if (packages.Count == 0)
            {
                throw new InvalidDataException("O arquivo não contém nenhum pacote.");
            }

            // Perfil exportado usa "name"; snapshot do Backup usa "label".
            var profileName = ReadString(root, "name") ?? ReadString(root, "label")
                ?? Path.GetFileNameWithoutExtension(path);
            var description = ReadString(root, "description") ?? string.Empty;

            var existingNames = (await _repository.GetAllAsync(ct).ConfigureAwait(false))
                .Select(item => item.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var finalName = profileName;
            for (var suffix = 2; existingNames.Contains(finalName); suffix++)
            {
                finalName = $"{profileName} ({suffix})";
            }

            return await SaveAsync(
                new Profile(Guid.Empty, finalName, description, DateTime.UtcNow, packages), ct).ConfigureAwait(false);
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        TryGetProperty(root, out var value, name) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static bool TryGetProperty(JsonElement element, out JsonElement value, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
    private async Task<Profile> FindAsync(Guid profileId, CancellationToken ct) =>
        (await _repository.GetAllAsync(ct).ConfigureAwait(false)).SingleOrDefault(item => item.Id == profileId)
        ?? throw new InvalidOperationException($"Perfil '{profileId}' não foi encontrado.");

    private static string SingleLine(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    public async Task<IReadOnlyList<UpdateResult>> InstallProfileAsync(
        Guid profileId,
        IProgress<(string packageId, int current, int total)>? progress = null,
        CancellationToken ct = default,
        IReadOnlyCollection<string>? selectedPackageIds = null)
    {
        var profile = (await _repository.GetAllAsync(ct).ConfigureAwait(false))
            .SingleOrDefault(item => item.Id == profileId)
            ?? throw new InvalidOperationException($"Perfil '{profileId}' não foi encontrado.");
        IReadOnlyList<ProfilePackage> packages = profile.Packages;
        if (selectedPackageIds is not null)
        {
            var selected = new HashSet<string>(selectedPackageIds, StringComparer.OrdinalIgnoreCase);
            packages = packages.Where(item => selected.Contains(item.Id)).ToList();
        }

        var results = new List<UpdateResult>(packages.Count);

        for (var index = 0; index < packages.Count; index++)
        {
            var package = packages[index];
            progress?.Report((package.Id, index + 1, packages.Count));
            var result = await _wingetService.InstallPackageAsync(package.Id, null, ct).ConfigureAwait(false);
            results.Add(result);
            await _historyRepository.AddAsync(result, ct).ConfigureAwait(false);
        }

        return results;
    }

    private async Task<Profile> SaveProfileAsync(Profile profile, CancellationToken ct)
    {
        await _repository.SaveAsync(profile, ct).ConfigureAwait(false);
        return profile;
    }

    private static Profile CreateDevelopmentProfile() => new(
        Guid.NewGuid(),
        "Desenvolvimento",
        "Ferramentas essenciais para desenvolvimento.",
        DateTime.UtcNow,
        [
            new ProfilePackage("Git.Git", "Git"),
            new ProfilePackage("Microsoft.VisualStudioCode", "Visual Studio Code"),
            new ProfilePackage("Microsoft.PowerShell", "PowerShell")
        ]);

    private static Profile CreateUtilitiesProfile() => new(
        Guid.NewGuid(),
        "Utilitários",
        "Ferramentas úteis para o dia a dia.",
        DateTime.UtcNow,
        [
            new ProfilePackage("7zip.7zip", "7-Zip"),
            new ProfilePackage("Microsoft.PowerToys", "PowerToys")
        ]);
}
