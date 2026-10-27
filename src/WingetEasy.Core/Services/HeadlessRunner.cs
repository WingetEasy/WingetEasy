using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

/// <summary>
/// Executa comandos em modo headless a partir dos argumentos CLI.
/// </summary>
public sealed class HeadlessRunner
{
    private readonly IWingetService _wingetService;
    private readonly IUpdateService _updateService;
    private readonly IBackupService _backupService;
    private readonly IBackupSnapshotRepository _backupSnapshotRepository;
    private readonly IToastNotificationService _toastService;
    private readonly ILogger<HeadlessRunner> _logger;
    private readonly ISettingsRepository? _settingsRepository;

    public HeadlessRunner(
        IWingetService wingetService,
        IUpdateService updateService,
        IBackupService backupService,
        IBackupSnapshotRepository backupSnapshotRepository,
        IToastNotificationService toastService,
        ILogger<HeadlessRunner> logger,
        ISettingsRepository? settingsRepository = null)
    {
        _wingetService = wingetService;
        _updateService = updateService;
        _backupService = backupService;
        _backupSnapshotRepository = backupSnapshotRepository;
        _toastService = toastService;
        _logger = logger;
        _settingsRepository = settingsRepository;
    }

    public async Task<int> RunAsync(CliCommand command, CancellationToken ct = default)
    {
        try
        {
            return command.Type switch
            {
                CliCommandType.Check => await RunCheckAsync(ct).ConfigureAwait(false),
                CliCommandType.UpdateAll => await RunUpdateAllAsync(ct).ConfigureAwait(false),
                CliCommandType.Backup => await RunBackupAsync(command.Value, ct).ConfigureAwait(false),
                CliCommandType.Restore => await RunRestoreAsync(command.Value, ct).ConfigureAwait(false),
                _ => 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao executar comando headless {CommandType}.", command.Type);
            return 1;
        }
    }

    private async Task<int> RunCheckAsync(CancellationToken ct)
    {
        _logger.LogInformation("Executando comando --check.");
        var updates = (await _wingetService.CheckUpdatesAsync(ct).ConfigureAwait(false)).ToList();
        _logger.LogInformation("Verificação concluída com {Count} atualização(ões).", updates.Count);

        if (updates.Count == 0)
        {
            return 0;
        }

        bool autoUpdate = false;
        bool notifyBefore = true;

        if (_settingsRepository != null)
        {
            var autoRaw = await _settingsRepository.GetAsync("AutoUpdate", ct).ConfigureAwait(false);
            autoUpdate = bool.TryParse(autoRaw, out var aParsed) && aParsed;

            var notifyRaw = await _settingsRepository.GetAsync("NotifyBeforeUpdate", ct).ConfigureAwait(false);
            notifyBefore = !bool.TryParse(notifyRaw, out var nParsed) || nParsed; // default true
        }

        if (autoUpdate)
        {
            _logger.LogInformation("AutoUpdate ativado; disparando atualização automática de {Count} pacotes...", updates.Count);
            if (notifyBefore)
            {
                _toastService.Register();
                _toastService.ShowUpdatesAvailable(updates.Count);
            }

            var metadata = updates.ToDictionary(
                p => p.Id,
                p => new PackageUpdateMetadata(p.Name, p.CurrentVersion, p.AvailableVersion),
                StringComparer.OrdinalIgnoreCase);

            var results = await _updateService
                .ExecuteUpdatesAsync(updates.Select(i => i.Id), null, null, null, isAutoUpdate: true, ct, null, metadata)
                .ConfigureAwait(false);

            var successCount = results.Count(r => r.Success);
            var failedCount = results.Count(r => !r.Success && !r.Skipped);

            _logger.LogInformation("AutoUpdate headless finalizado: {Success} com sucesso, {Failed} com falha.", successCount, failedCount);
            if (successCount > 0 || failedCount > 0)
            {
                _toastService.Register();
                _toastService.ShowUpdateComplete(successCount, failedCount);
            }
        }
        else
        {
            _toastService.Register();
            _toastService.ShowUpdatesAvailable(updates.Count);
        }

        return 0;
    }

    private async Task<int> RunUpdateAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("Executando comando --update-all.");
        var updates = (await _wingetService.CheckUpdatesAsync(ct).ConfigureAwait(false)).ToList();
        if (updates.Count == 0)
        {
            _logger.LogInformation("Nenhuma atualização disponível para --update-all.");
            return 0;
        }

        var metadata = updates.ToDictionary(
            p => p.Id,
            p => new PackageUpdateMetadata(p.Name, p.CurrentVersion, p.AvailableVersion),
            StringComparer.OrdinalIgnoreCase);

        var results = await _updateService
            .ExecuteUpdatesAsync(updates.Select(item => item.Id), null, null, null, isAutoUpdate: false, ct, null, metadata)
            .ConfigureAwait(false);

        var failedCount = results.Count(result => !result.Success && !result.Skipped);
        var skippedCount = results.Count(result => result.Skipped);
        _logger.LogInformation("Atualização em lote concluída com {Success} sucesso(s), {Skipped} adiado(s) e {Failed} falha(s).",
            results.Count - failedCount - skippedCount,
            skippedCount,
            failedCount);

        return failedCount > 0 ? 2 : 0;
    }

    private async Task<int> RunBackupAsync(string? exportPath, CancellationToken ct)
    {
        _logger.LogInformation("Executando comando --backup.");
        var snapshot = await _backupService
            .CreateSnapshotAsync($"Headless {DateTime.Now:yyyy-MM-dd HH:mm:ss}", ct)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(exportPath))
        {
            _logger.LogInformation("Snapshot {SnapshotId} criado sem exportação de arquivo.", snapshot.Id);
            return 0;
        }

        var extension = Path.GetExtension(exportPath).ToLowerInvariant();
        if (extension == ".json")
        {
            await _backupService.ExportJsonAsync(snapshot.Id, exportPath, ct).ConfigureAwait(false);
            return 0;
        }

        if (extension == ".ps1")
        {
            await _backupService.ExportScriptAsync(snapshot.Id, exportPath, ct).ConfigureAwait(false);
            return 0;
        }

        _logger.LogError("Extensão de exportação não suportada para --backup: {Path}", exportPath);
        return 1;
    }

    private async Task<int> RunRestoreAsync(string? value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogError("Comando --restore sem argumento.");
            return 1;
        }

        _logger.LogInformation("Executando comando --restore para {Value}.", value);
        Guid snapshotId;

        if (Guid.TryParse(value, out var parsedId))
        {
            snapshotId = parsedId;
        }
        else
        {
            if (!File.Exists(value))
            {
                _logger.LogError("Arquivo de snapshot não encontrado: {Path}", value);
                return 1;
            }

            var extension = Path.GetExtension(value).ToLowerInvariant();
            if (extension != ".json")
            {
                _logger.LogError("A restauração via arquivo suporta apenas .json. Caminho: {Path}", value);
                return 1;
            }

            var json = await File.ReadAllTextAsync(value, ct).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize(json, CoreJsonContext.Default.BackupSnapshot);
            if (snapshot is null)
            {
                _logger.LogError("Snapshot inválido em {Path}.", value);
                return 1;
            }

            snapshotId = snapshot.Id == Guid.Empty ? Guid.NewGuid() : snapshot.Id;
            var toPersist = snapshot with { Id = snapshotId };
            await _backupSnapshotRepository.SaveAsync(toPersist, ct).ConfigureAwait(false);
        }

        var results = await _backupService.RestoreSnapshotAsync(snapshotId, null, 1, ct).ConfigureAwait(false);
        var failedCount = results.Count(result => !result.Success);

        return failedCount > 0 ? 2 : 0;
    }
}
