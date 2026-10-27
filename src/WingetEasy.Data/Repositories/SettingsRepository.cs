using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public event EventHandler<string>? SettingChanged;

    [ActivatorUtilitiesConstructor]
    public SettingsRepository(IDbContextFactory<AppDbContext> contextFactory) => _contextFactory = contextFactory;

    public SettingsRepository(AppDbContext dbContext)
    {
        _contextFactory = new AppDbContext.DelegatingFactory(dbContext.Database.GetConnectionString());
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var setting = await _dbContext.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct).ConfigureAwait(false);
        return setting?.Value;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        using var _dbContext = _contextFactory.CreateDbContext();
        var setting = await _dbContext.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct).ConfigureAwait(false);
        if (setting == null)
        {
            _dbContext.Settings.Add(new SettingEntity { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            _dbContext.Settings.Update(setting);
        }
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            SettingChanged?.Invoke(this, key);
        }
        catch
        {
            // Observadores não devem quebrar a gravação do banco
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = await GetAsync(key, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(value)) return null;

        try
        {
            if (CoreJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
            {
                return JsonSerializer.Deserialize(value, typeInfo);
            }

            return JsonSerializer.Deserialize<T>(value, JsonSerializerOptions.Default);
        }
        catch
        {
            return null; // Retorna nulo se o JSON for inválido no banco
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default) where T : class
    {
        var json = CoreJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo
            ? JsonSerializer.Serialize(value, typeInfo)
            : JsonSerializer.Serialize(value, JsonSerializerOptions.Default);
        await SetAsync(key, json, ct).ConfigureAwait(false);
    }
}
