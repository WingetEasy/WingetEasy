using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Core.Services;

namespace WingetEasy.Core.Tests;

public sealed class VelopackAppUpdateServiceTests
{
    [Fact]
    public async Task CheckForAppUpdateAsync_ReturnsNullWhenNotInstalled()
    {
        var manager = new Mock<IAppUpdateManager>();
        manager.SetupGet(item => item.IsInstalled).Returns(false);
        var service = CreateService(manager);

        var result = await service.CheckForAppUpdateAsync();

        result.Should().BeNull();
        manager.Verify(item => item.CheckForUpdatesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckForAppUpdateAsync_ReturnsDetectedVersion()
    {
        var update = new AppUpdateInfo("2.0.0");
        var manager = InstalledManager();
        manager.Setup(item => item.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(update);
        var service = CreateService(manager);

        var result = await service.CheckForAppUpdateAsync();

        result.Should().Be(update);
    }

    [Fact]
    public async Task DownloadAppUpdateAsync_DelegatesToManager()
    {
        var update = new AppUpdateInfo("2.0.0");
        var manager = InstalledManager();
        var service = CreateService(manager);

        await service.DownloadAppUpdateAsync(update);

        manager.Verify(item => item.DownloadUpdatesAsync(update, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ApplyUpdateAndRestart_DelegatesToManager()
    {
        var update = new AppUpdateInfo("2.0.0");
        var manager = InstalledManager();
        var service = CreateService(manager);

        service.ApplyUpdateAndRestart(update);

        manager.Verify(item => item.ApplyUpdatesAndRestart(update), Times.Once);
    }

    private static VelopackAppUpdateService CreateService(Mock<IAppUpdateManager> manager) =>
        new(manager.Object, NullLogger<VelopackAppUpdateService>.Instance);

    private static Mock<IAppUpdateManager> InstalledManager()
    {
        var manager = new Mock<IAppUpdateManager>();
        manager.SetupGet(item => item.IsInstalled).Returns(true);
        return manager;
    }
}
