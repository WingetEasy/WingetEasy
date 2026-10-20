using System;
using System.Collections.Generic;
using System.Linq;
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

    public async Task<IReadOnlyList<UpdateResult>> InstallProfileAsync(
        Guid profileId,
        IProgress<(string packageId, int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var profile = (await _repository.GetAllAsync(ct).ConfigureAwait(false))
            .SingleOrDefault(item => item.Id == profileId)
            ?? throw new InvalidOperationException($"Perfil '{profileId}' não foi encontrado.");
        var results = new List<UpdateResult>(profile.Packages.Count);

        for (var index = 0; index < profile.Packages.Count; index++)
        {
            var package = profile.Packages[index];
            progress?.Report((package.Id, index + 1, profile.Packages.Count));
            var result = await _wingetService.InstallUpdateAsync(package.Id, null, ct).ConfigureAwait(false);
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
