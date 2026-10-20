using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Gerencia perfis e a instalação dos pacotes associados.
/// </summary>
public interface IProfilesService
{
    Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken ct = default);
    Task<Profile> SaveAsync(Profile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UpdateResult>> InstallProfileAsync(
        Guid profileId,
        IProgress<(string packageId, int current, int total)>? progress = null,
        CancellationToken ct = default);
}
