using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Persiste os perfis de pacotes do utilizador.
/// </summary>
public interface IProfileRepository
{
    Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(Profile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
