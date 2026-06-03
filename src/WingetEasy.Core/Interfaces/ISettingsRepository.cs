using System.Threading;
using System.Threading.Tasks;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Contrato para guardar e recuperar configurações do utilizador (ex: tema, idioma, preferências).
/// </summary>

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, CancellationToken ct = default) where T : class;
}
