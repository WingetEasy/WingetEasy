using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Core.Services;
using WingetEasy.Data.Repositories;
using Xunit;

namespace WingetEasy.Core.Tests;

public class SettingsActivationTests : RepositoryTestBase
{
    private readonly SettingsRepository _settingsRepo;

    public SettingsActivationTests()
    {
        _settingsRepo = new SettingsRepository(Db);
    }

    [Fact]
    public async Task SettingsRepository_SetAsync_FiresSettingChangedEvent()
    {
        // Arrange
        string? changedKey = null;
        _settingsRepo.SettingChanged += (_, key) => changedKey = key;

        // Act
        await _settingsRepo.SetAsync("AutoUpdate", "true");

        // Assert
        changedKey.Should().Be("AutoUpdate");
        (await _settingsRepo.GetAsync("AutoUpdate")).Should().Be("true");
    }

    [Theory]
    [InlineData("1.0.0-beta.1", null, true)]
    [InlineData("2.0.0-rc2", null, true)]
    [InlineData("1.0.0-preview", null, true)]
    [InlineData("0.5.0-alpha", null, true)]
    [InlineData("1.0.0-dev", null, true)]
    [InlineData("3.0.0-canary", null, true)]
    [InlineData("1.1.0-insider", null, true)]
    [InlineData("2.2.0-ea", null, true)]
    [InlineData("1.2.3", null, false)]
    [InlineData("2024.1.2", null, false)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.1", false)] // Já está em beta, não deve ser filtrado
    public void WingetService_IsBetaOrPreRelease_ClassifiesCorrectly(string availableVersion, string? currentVersion, bool expectedBeta)
    {
        var result = WingetService.IsBetaOrPreRelease(availableVersion, currentVersion);
        result.Should().Be(expectedBeta);
    }

    [Fact]
    public async Task WingetService_CheckUpdates_WhenIncludeBetaVersionsIsFalse_FiltersBetaUpdates()
    {
        // Arrange
        var pkgRepo = new Mock<IPackageRepository>();
        pkgRepo.Setup(r => r.GetSkippedIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var logger = new Mock<ILogger<WingetService>>();
        var settings = new Mock<ISettingsRepository>();
        settings.Setup(s => s.GetAsync("IncludeBetaVersions", It.IsAny<CancellationToken>()))
            .ReturnsAsync("false");

        var tableOutput =
            "Nome                  Id                    Versão   Disponível Origem\r\n" +
            "----------------------------------------------------------------------\r\n" +
            "Stable App            Vendor.Stable         1.0.0    2.0.0      winget\r\n" +
            "Beta App              Vendor.Beta           1.0.0    2.0.0-beta winget\r\n" +
            "2 atualizações disponíveis.\r\n";

        var service = new WingetService(
            pkgRepo.Object,
            logger.Object,
            (cmd, ct) => Task.FromResult((0, tableOutput, string.Empty)),
            adminElevationHelper: null,
            elevatedRepairRunner: null,
            settingsRepository: settings.Object);

        // Act
        var updates = (await service.CheckUpdatesAsync()).ToList();

        // Assert
        updates.Should().HaveCount(1);
        updates[0].Id.Should().Be("Vendor.Stable");
    }

    [Fact]
    public async Task WingetService_CheckUpdates_WhenIncludeBetaVersionsIsTrue_IncludesBetaUpdates()
    {
        // Arrange
        var pkgRepo = new Mock<IPackageRepository>();
        pkgRepo.Setup(r => r.GetSkippedIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var logger = new Mock<ILogger<WingetService>>();
        var settings = new Mock<ISettingsRepository>();
        settings.Setup(s => s.GetAsync("IncludeBetaVersions", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");

        var tableOutput =
            "Nome                  Id                    Versão   Disponível Origem\r\n" +
            "----------------------------------------------------------------------\r\n" +
            "Stable App            Vendor.Stable         1.0.0    2.0.0      winget\r\n" +
            "Beta App              Vendor.Beta           1.0.0    2.0.0-beta winget\r\n" +
            "2 atualizações disponíveis.\r\n";

        var service = new WingetService(
            pkgRepo.Object,
            logger.Object,
            (cmd, ct) => Task.FromResult((0, tableOutput, string.Empty)),
            adminElevationHelper: null,
            elevatedRepairRunner: null,
            settingsRepository: settings.Object);

        // Act
        var updates = (await service.CheckUpdatesAsync()).ToList();

        // Assert
        updates.Should().HaveCount(2);
        updates.Select(u => u.Id).Should().Contain(["Vendor.Stable", "Vendor.Beta"]);
    }

    [Fact]
    public async Task UpdateService_ExecuteUpdatesAsync_RecordsFromAndToVersionsInHistory()
    {
        // Arrange
        var wingetMock = new Mock<IWingetService>();
        wingetMock.Setup(w => w.InstallUpdateAsync(
                "App.Test",
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<string>>()))
            .ReturnsAsync(new UpdateResult("App.Test", "App.Test", true));

        var historyRepo = new Mock<IUpdateHistoryRepository>();
        UpdateResult? capturedResult = null;
        historyRepo.Setup(h => h.AddAsync(It.IsAny<UpdateResult>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateResult, CancellationToken>((r, _) => capturedResult = r)
            .Returns(Task.CompletedTask);

        var adminMock = new Mock<IAdminElevationHelper>();
        var runningProcessMock = new Mock<IRunningProcessService>();
        runningProcessMock.Setup(r => r.IsAppRunningWithVisibleWindow(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(false);

        var updateService = new UpdateService(
            wingetMock.Object,
            historyRepo.Object,
            adminMock.Object,
            runningProcessMock.Object);

        var metadata = new Dictionary<string, PackageUpdateMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Test"] = new PackageUpdateMetadata("Friendly Test App", "1.0.0", "1.5.0")
        };

        // Act
        var results = await updateService.ExecuteUpdatesAsync(
            ["App.Test"],
            progress: null,
            confirmElevation: null,
            handleRunningApp: null,
            isAutoUpdate: false,
            ct: default,
            phaseProgress: null,
            packageMetadata: metadata);

        // Assert
        results.Should().HaveCount(1);
        results[0].PackageName.Should().Be("Friendly Test App");
        results[0].FromVersion.Should().Be("1.0.0");
        results[0].ToVersion.Should().Be("1.5.0");

        capturedResult.Should().NotBeNull();
        capturedResult!.PackageName.Should().Be("Friendly Test App");
        capturedResult.FromVersion.Should().Be("1.0.0");
        capturedResult.ToVersion.Should().Be("1.5.0");
    }

    [Fact]
    public async Task HeadlessRunner_RunCheckAsync_WhenAutoUpdateEnabled_ExecutesUpdates()
    {
        // Arrange
        var wingetMock = new Mock<IWingetService>();
        wingetMock.Setup(w => w.CheckUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WingetPackage("App.A", "App A", "1.0", "2.0", "winget")
            ]);

        var updateMock = new Mock<IUpdateService>();
        updateMock.Setup(u => u.ExecuteUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IProgress<(string name, int pct, int cur, int total)>>(),
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<Func<string, Task<AppRunningResolution>>>(),
                true, // isAutoUpdate
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<(string packageId, string phase)>>(),
                It.IsAny<IReadOnlyDictionary<string, PackageUpdateMetadata>>()))
            .ReturnsAsync([
                new UpdateResult("App.A", "App A", true, null, TimeSpan.FromSeconds(1), FromVersion: "1.0", ToVersion: "2.0")
            ]);

        var toastMock = new Mock<IToastNotificationService>();
        var settingsMock = new Mock<ISettingsRepository>();
        settingsMock.Setup(s => s.GetAsync("AutoUpdate", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        settingsMock.Setup(s => s.GetAsync("NotifyBeforeUpdate", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");

        var backupMock = new Mock<IBackupService>();
        var snapshotRepoMock = new Mock<IBackupSnapshotRepository>();
        var loggerMock = new Mock<ILogger<HeadlessRunner>>();

        var runner = new HeadlessRunner(
            wingetMock.Object,
            updateMock.Object,
            backupMock.Object,
            snapshotRepoMock.Object,
            toastMock.Object,
            loggerMock.Object,
            settingsMock.Object);

        // Act
        var code = await runner.RunAsync(new CliCommand(CliCommandType.Check));

        // Assert
        code.Should().Be(0);
        // Deve notificar antes
        toastMock.Verify(t => t.ShowUpdatesAvailable(1), Times.Once);
        // Deve executar as atualizações com isAutoUpdate = true
        updateMock.Verify(u => u.ExecuteUpdatesAsync(
            It.Is<IEnumerable<string>>(ids => ids.Contains("App.A")),
            null,
            null,
            null,
            true,
            It.IsAny<CancellationToken>(),
            null,
            It.Is<IReadOnlyDictionary<string, PackageUpdateMetadata>>(m => m.ContainsKey("App.A"))), Times.Once);
        // Deve notificar conclusão
        toastMock.Verify(t => t.ShowUpdateComplete(1, 0), Times.Once);
    }

    [Fact]
    public async Task HeadlessRunner_RunCheckAsync_WhenNotifyBeforeUpdateIsFalse_SkipsPreNotification()
    {
        // Arrange
        var wingetMock = new Mock<IWingetService>();
        wingetMock.Setup(w => w.CheckUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WingetPackage("App.A", "App A", "1.0", "2.0", "winget")
            ]);

        var updateMock = new Mock<IUpdateService>();
        updateMock.Setup(u => u.ExecuteUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IProgress<(string name, int pct, int cur, int total)>>(),
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<Func<string, Task<AppRunningResolution>>>(),
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<(string packageId, string phase)>>(),
                It.IsAny<IReadOnlyDictionary<string, PackageUpdateMetadata>>()))
            .ReturnsAsync([
                new UpdateResult("App.A", "App A", true, null, TimeSpan.FromSeconds(1))
            ]);

        var toastMock = new Mock<IToastNotificationService>();
        var settingsMock = new Mock<ISettingsRepository>();
        settingsMock.Setup(s => s.GetAsync("AutoUpdate", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        settingsMock.Setup(s => s.GetAsync("NotifyBeforeUpdate", It.IsAny<CancellationToken>()))
            .ReturnsAsync("false"); // Desativado!

        var backupMock = new Mock<IBackupService>();
        var snapshotRepoMock = new Mock<IBackupSnapshotRepository>();
        var loggerMock = new Mock<ILogger<HeadlessRunner>>();

        var runner = new HeadlessRunner(
            wingetMock.Object,
            updateMock.Object,
            backupMock.Object,
            snapshotRepoMock.Object,
            toastMock.Object,
            loggerMock.Object,
            settingsMock.Object);

        // Act
        var code = await runner.RunAsync(new CliCommand(CliCommandType.Check));

        // Assert
        code.Should().Be(0);
        // NÃO deve notificar antes
        toastMock.Verify(t => t.ShowUpdatesAvailable(It.IsAny<int>()), Times.Never);
        // Deve notificar conclusão
        toastMock.Verify(t => t.ShowUpdateComplete(1, 0), Times.Once);
    }

    [Fact]
    public async Task HeadlessRunner_RunCheckAsync_WhenAutoUpdateDisabled_OnlyNotifiesAndDoesNotUpdate()
    {
        // Arrange
        var wingetMock = new Mock<IWingetService>();
        wingetMock.Setup(w => w.CheckUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WingetPackage("App.A", "App A", "1.0", "2.0", "winget")
            ]);

        var updateMock = new Mock<IUpdateService>();
        var toastMock = new Mock<IToastNotificationService>();
        var settingsMock = new Mock<ISettingsRepository>();
        settingsMock.Setup(s => s.GetAsync("AutoUpdate", It.IsAny<CancellationToken>()))
            .ReturnsAsync("false");

        var backupMock = new Mock<IBackupService>();
        var snapshotRepoMock = new Mock<IBackupSnapshotRepository>();
        var loggerMock = new Mock<ILogger<HeadlessRunner>>();

        var runner = new HeadlessRunner(
            wingetMock.Object,
            updateMock.Object,
            backupMock.Object,
            snapshotRepoMock.Object,
            toastMock.Object,
            loggerMock.Object,
            settingsMock.Object);

        // Act
        var code = await runner.RunAsync(new CliCommand(CliCommandType.Check));

        // Assert
        code.Should().Be(0);
        toastMock.Verify(t => t.ShowUpdatesAvailable(1), Times.Once);
        updateMock.Verify(u => u.ExecuteUpdatesAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<IProgress<(string, int, int, int)>>(),
            It.IsAny<Func<Task<bool>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
