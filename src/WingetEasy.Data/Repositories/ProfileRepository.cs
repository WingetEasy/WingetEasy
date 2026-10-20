using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Data.Repositories;

public sealed class ProfileRepository : IProfileRepository
{
    private const string ProfilesKey = "Profiles";
    private readonly ISettingsRepository _settingsRepository;

    public ProfileRepository(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken ct = default)
    {
        var json = await _settingsRepository.GetAsync(ProfilesKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.ListProfile) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task SaveAsync(Profile profile, CancellationToken ct = default)
    {
        var profiles = (await GetAllAsync(ct).ConfigureAwait(false)).ToList();
        var index = profiles.FindIndex(item => item.Id == profile.Id);
        if (index >= 0)
        {
            profiles[index] = profile;
        }
        else
        {
            profiles.Add(profile);
        }

        await _settingsRepository.SetAsync(
            ProfilesKey,
            JsonSerializer.Serialize(profiles, CoreJsonContext.Default.ListProfile),
            ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var profiles = (await GetAllAsync(ct).ConfigureAwait(false))
            .Where(profile => profile.Id != id)
            .ToList();
        await _settingsRepository.SetAsync(
            ProfilesKey,
            JsonSerializer.Serialize(profiles, CoreJsonContext.Default.ListProfile),
            ct).ConfigureAwait(false);
    }
}
