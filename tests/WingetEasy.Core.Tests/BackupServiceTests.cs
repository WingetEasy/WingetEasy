using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Core.Services;

namespace WingetEasy.Core.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task CreateSnapshotAsync_StoresDistinctInstalledPackages()
    {
        var winget = new Mock<IWingetService>();
        winget.Setup(service => service.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WingetPackage("Contoso.App", "Contoso", "1.0", "1.0", "winget"),
                new WingetPackage("contoso.app", "Contoso", "1.0", "1.0", "winget")
            });
        winget.Setup(service => service.GetWingetVersionAsync()).ReturnsAsync("1.8.0");
        var snapshots = new Mock<IBackupSnapshotRepository>();
        var history = new Mock<IUpdateHistoryRepository>();
        var service = new BackupService(winget.Object, snapshots.Object, history.Object);

        var snapshot = await service.CreateSnapshotAsync("  Meu snapshot  ");

        snapshot.Label.Should().Be("Meu snapshot");
        snapshot.PackageCount.Should().Be(1);
        snapshot.Packages.Should().ContainSingle(package => package.Id == "Contoso.App");
        snapshots.Verify(repository => repository.SaveAsync(snapshot, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreSnapshotAsync_RetriesFailuresAndRecordsResults()
    {
        var snapshot = new BackupSnapshot(
            Guid.NewGuid(),
            "Teste",
            DateTime.UtcNow,
            1,
            "1.8",
            [new BackupPackage("Contoso.App", "Contoso", "1.0")]);
        var snapshots = new Mock<IBackupSnapshotRepository>();
        snapshots.Setup(repository => repository.GetAsync(snapshot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var winget = new Mock<IWingetService>();
        winget.SetupSequence(service => service.InstallUpdateAsync(
                "Contoso.App",
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult("Contoso.App", "Contoso", false, "falha"))
            .ReturnsAsync(new UpdateResult("Contoso.App", "Contoso", true));
        var history = new Mock<IUpdateHistoryRepository>();
        var service = new BackupService(winget.Object, snapshots.Object, history.Object);

        var result = await service.RestoreSnapshotAsync(snapshot.Id, maxRetries: 1);

        result.Should().ContainSingle().Which.Success.Should().BeTrue();
        winget.Verify(call => call.InstallUpdateAsync(
            "Contoso.App",
            It.IsAny<IProgress<int>?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        history.Verify(repository => repository.AddAsync(
            It.Is<UpdateResult>(update => update.Success),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportScriptAsync_WritesInstallCommands()
    {
        var snapshot = new BackupSnapshot(
            Guid.NewGuid(),
            "Teste",
            DateTime.UtcNow,
            1,
            "1.8",
            [new BackupPackage("Contoso.App", "Contoso", "1.0")]);
        var snapshots = new Mock<IBackupSnapshotRepository>();
        snapshots.Setup(repository => repository.GetAsync(snapshot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid()}.ps1");
        var service = new BackupService(
            Mock.Of<IWingetService>(),
            snapshots.Object,
            Mock.Of<IUpdateHistoryRepository>());

        try
        {
            await service.ExportScriptAsync(snapshot.Id, path);
            var script = await System.IO.File.ReadAllTextAsync(path);
            script.Should().Contain("winget install --id \"Contoso.App\" --exact");
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ExportJsonAsync_WritesSnapshotData()
    {
        var snapshot = new BackupSnapshot(
            Guid.NewGuid(),
            "Teste",
            DateTime.UtcNow,
            1,
            "1.8",
            [new BackupPackage("Contoso.App", "Contoso", "1.0")]);
        var snapshots = new Mock<IBackupSnapshotRepository>();
        snapshots.Setup(repository => repository.GetAsync(snapshot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var service = new BackupService(
            Mock.Of<IWingetService>(),
            snapshots.Object,
            Mock.Of<IUpdateHistoryRepository>());

        try
        {
            await service.ExportJsonAsync(snapshot.Id, path);
            var json = await System.IO.File.ReadAllTextAsync(path);
            json.Should().Contain("Contoso.App");
            json.Should().Contain("Teste", because: "the exported file should preserve snapshot metadata");
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToSnapshotRepository()
    {
        var snapshotId = Guid.NewGuid();
        var repository = new Mock<IBackupSnapshotRepository>();
        var service = new BackupService(
            Mock.Of<IWingetService>(),
            repository.Object,
            Mock.Of<IUpdateHistoryRepository>());

        await service.DeleteAsync(snapshotId.ToString());

        repository.Verify(
            item => item.DeleteAsync(snapshotId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void SnapshotSerialization_RoundTripsMetadataAndPackages()
    {
        var snapshot = new BackupSnapshot(
            Guid.NewGuid(),
            "Teste",
            DateTime.UtcNow,
            1,
            "1.8",
            [new BackupPackage("Contoso.App", "Contoso", "1.0")]);

        var json = BackupService.Serialize(snapshot);
        var parsed = BackupService.Deserialize(json);

        parsed.Should().BeEquivalentTo(snapshot);
    }
}
