using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Core.Services;

namespace WingetEasy.Core.Tests;

public sealed class CliArgumentsParserTests
{
    [Fact]
    public void TryParse_ShouldParseCheck()
    {
        var parser = new CliArgumentsParser();

        var ok = parser.TryParse(["app.exe", "--check"], out var command, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        command.Type.Should().Be(CliCommandType.Check);
    }

    [Fact]
    public void TryParse_ShouldParseUpdateAll()
    {
        var parser = new CliArgumentsParser();

        var ok = parser.TryParse(["--update-all"], out var command, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        command.Type.Should().Be(CliCommandType.UpdateAll);
    }

    [Fact]
    public void TryParse_ShouldParseBackupWithOptionalPath()
    {
        var parser = new CliArgumentsParser();

        var ok = parser.TryParse(["--backup", "C:\\tmp\\snapshot.json"], out var command, out _);

        ok.Should().BeTrue();
        command.Type.Should().Be(CliCommandType.Backup);
        command.Value.Should().Be("C:\\tmp\\snapshot.json");
    }

    [Fact]
    public void TryParse_ShouldParseRestorePath()
    {
        var parser = new CliArgumentsParser();

        var ok = parser.TryParse(["--restore", "C:\\tmp\\snapshot.json"], out var command, out _);

        ok.Should().BeTrue();
        command.Type.Should().Be(CliCommandType.Restore);
        command.Value.Should().Be("C:\\tmp\\snapshot.json");
    }

    [Fact]
    public void TryParse_ShouldParseMinimized()
    {
        var parser = new CliArgumentsParser();

        var ok = parser.TryParse(["--minimized"], out var command, out _);

        ok.Should().BeTrue();
        command.Type.Should().Be(CliCommandType.Minimized);
    }

    [Fact]
    public void TryParse_ShouldFailOnMissingRestoreArgument()
    {
        var parser = new CliArgumentsParser();

        var ok = parser.TryParse(["--restore"], out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryParse_ShouldFailOnMultipleHeadlessCommands()
    {
        var parser = new CliArgumentsParser();

        var ok = parser.TryParse(["--check", "--update-all"], out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Somente um comando");
    }
}

public sealed class HeadlessRunnerTests
{
    [Fact]
    public async Task RunAsync_Check_ShouldNotifyAndReturnZero()
    {
        var winget = new Mock<IWingetService>();
        winget.Setup(service => service.CheckUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WingetPackage("A", "A", "1", "2", "winget")]);
        var toast = new Mock<IToastNotificationService>();
        var runner = CreateRunner(winget: winget, toast: toast);

        var code = await runner.RunAsync(new CliCommand(CliCommandType.Check));

        code.Should().Be(0);
        toast.Verify(service => service.Register(), Times.Once);
        toast.Verify(service => service.ShowUpdatesAvailable(1), Times.Once);
    }

    [Fact]
    public async Task RunAsync_UpdateAll_ShouldReturnPartialFailureCode()
    {
        var winget = new Mock<IWingetService>();
        winget.Setup(service => service.CheckUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WingetPackage("A", "A", "1", "2", "winget"),
                new WingetPackage("B", "B", "1", "2", "winget")
            ]);
        var update = new Mock<IUpdateService>();
        update.Setup(service => service.ExecuteUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IProgress<(string name, int pct, int cur, int total)>>(),
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UpdateResult("A", "A", true),
                new UpdateResult("B", "B", false, "erro")
            ]);
        var runner = CreateRunner(winget: winget, update: update);

        var code = await runner.RunAsync(new CliCommand(CliCommandType.UpdateAll));

        code.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_Backup_WithJsonPath_ShouldExportJson()
    {
        var backup = new Mock<IBackupService>();
        var snapshot = new BackupSnapshot(Guid.NewGuid(), "s", DateTime.UtcNow, 1, "1.0", []);
        backup.Setup(service => service.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var runner = CreateRunner(backup: backup);

        var code = await runner.RunAsync(new CliCommand(CliCommandType.Backup, "C:\\tmp\\backup.json"));

        code.Should().Be(0);
        backup.Verify(service => service.ExportJsonAsync(It.IsAny<Guid>(), "C:\\tmp\\backup.json", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_Restore_ById_ShouldReturnZeroOnSuccess()
    {
        var backup = new Mock<IBackupService>();
        backup.Setup(service => service.RestoreSnapshotAsync(
                It.IsAny<Guid>(),
                It.IsAny<IProgress<(string packageId, int current, int total)>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UpdateResult("A", "A", true)]);
        var id = Guid.NewGuid();
        var runner = CreateRunner(backup: backup);

        var code = await runner.RunAsync(new CliCommand(CliCommandType.Restore, id.ToString()));

        code.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_Backup_WithUnsupportedExtension_ShouldReturnErrorCode()
    {
        var backup = new Mock<IBackupService>();
        backup.Setup(service => service.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupSnapshot(Guid.NewGuid(), "s", DateTime.UtcNow, 1, "1.0", []));
        var runner = CreateRunner(backup: backup);

        var code = await runner.RunAsync(new CliCommand(CliCommandType.Backup, "C:\\tmp\\backup.txt"));

        code.Should().Be(1);
    }

    private static HeadlessRunner CreateRunner(
        Mock<IWingetService>? winget = null,
        Mock<IUpdateService>? update = null,
        Mock<IBackupService>? backup = null,
        Mock<IToastNotificationService>? toast = null,
        Mock<IBackupSnapshotRepository>? snapshotRepository = null)
    {
        if (winget is null)
        {
            winget = new Mock<IWingetService>();
            winget.Setup(service => service.CheckUpdatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }

        if (update is null)
        {
            update = new Mock<IUpdateService>();
            update.Setup(service => service.ExecuteUpdatesAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<IProgress<(string name, int pct, int cur, int total)>>() ,
                    It.IsAny<Func<Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        if (backup is null)
        {
            backup = new Mock<IBackupService>();
            backup.Setup(service => service.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BackupSnapshot(Guid.NewGuid(), "seed", DateTime.UtcNow, 0, "1.0", []));
            backup.Setup(service => service.RestoreSnapshotAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IProgress<(string packageId, int current, int total)>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        toast ??= new Mock<IToastNotificationService>();
        snapshotRepository ??= new Mock<IBackupSnapshotRepository>();

        return new HeadlessRunner(
            winget.Object,
            update.Object,
            backup.Object,
            snapshotRepository.Object,
            toast.Object,
            Mock.Of<ILogger<HeadlessRunner>>());
    }
}

