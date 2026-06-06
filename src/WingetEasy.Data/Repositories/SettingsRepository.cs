using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WingetEasy.Core.Interfaces;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _dbContext;

    public SettingsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var setting = await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == key, ct).ConfigureAwait(false);
        return setting?.Value;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == key, ct).ConfigureAwait(false);
        if (setting == null)
        {
            _dbContext.Settings.Add(new SettingEntity { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = await GetAsync(key, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(value)) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(value, JsonSerializerOptions.Default);
        }
        catch
        {
            return null; // Retorna nulo se o JSON for inválido no banco
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions.Default);
        await SetAsync(key, json, ct).ConfigureAwait(false);
    }
}
