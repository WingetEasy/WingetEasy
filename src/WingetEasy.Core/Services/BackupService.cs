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
/// Implementa a criação e restauração de snapshots de pacotes instalados.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly IWingetService _wingetService;
    private readonly IBackupSnapshotRepository _repository;
    private readonly IUpdateHistoryRepository _historyRepository;

    public BackupService(
        IWingetService wingetService,
        IBackupSnapshotRepository repository,
        IUpdateHistoryRepository historyRepository)
    {
        _wingetService = wingetService;
        _repository = repository;
        _historyRepository = historyRepository;
    }

    public async Task<BackupSnapshot> CreateSnapshotAsync(string label, CancellationToken ct = default)
    {
        var installed = await _wingetService.GetInstalledPackagesAsync(ct).ConfigureAwait(false);
        var packages = installed
            .Where(package => !string.IsNullOrWhiteSpace(package.Id))
            .Select(package => new BackupPackage(package.Id, package.Name, package.CurrentVersion))
            .DistinctBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var snapshot = new BackupSnapshot(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(label) ? "Snapshot" : label.Trim(),
            DateTime.UtcNow,
            packages.Count,
            await _wingetService.GetWingetVersionAsync().ConfigureAwait(false),
            packages);

        await _repository.SaveAsync(snapshot, ct).ConfigureAwait(false);
        return snapshot;
    }

    public async Task<IReadOnlyList<UpdateResult>> RestoreSnapshotAsync(
        Guid snapshotId,
        IProgress<(string packageId, int current, int total)>? progress = null,
        int maxRetries = 1,
        CancellationToken ct = default)
    {
        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        }

        var snapshot = await _repository.GetAsync(snapshotId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Snapshot '{snapshotId}' não foi encontrado.");
        var packages = snapshot.Packages ?? [];
        var results = new List<UpdateResult>(packages.Count);

        for (var index = 0; index < packages.Count; index++)
        {
            var package = packages[index];
            progress?.Report((package.Id, index + 1, packages.Count));

            UpdateResult result = new(package.Id, package.Name, false, "Restauração não executada.", TimeSpan.Zero);
            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                result = await _wingetService.InstallUpdateAsync(package.Id, null, ct).ConfigureAwait(false);
                if (result.Success)
                {
                    break;
                }
            }
            results.Add(result);
            await _historyRepository.AddAsync(result, ct).ConfigureAwait(false);
        }

        return results;
    }

    public async Task ExportScriptAsync(Guid snapshotId, string path, CancellationToken ct = default)
    {
        var snapshot = await _repository.GetAsync(snapshotId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Snapshot '{snapshotId}' não foi encontrado.");
        var builder = new StringBuilder();
        builder.AppendLine("# WingetEasy snapshot restore script");
        builder.AppendLine($"# Snapshot: {snapshot.Label}");
        builder.AppendLine($"# Created: {snapshot.CreatedAt:O}");
        builder.AppendLine();

        foreach (var package in snapshot.Packages ?? [])
        {
            builder.AppendLine($"winget install --id \"{EscapePowerShell(package.Id)}\" --exact --accept-source-agreements --accept-package-agreements");
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
    }

    internal static string Serialize(BackupSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, CoreJsonContext.Default.BackupSnapshot);

    internal static BackupSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize(json, CoreJsonContext.Default.BackupSnapshot)
        ?? throw new JsonException("O snapshot está vazio ou inválido.");

    private static string EscapePowerShell(string value) => value.Replace("\"", "`\"", StringComparison.Ordinal);
}
