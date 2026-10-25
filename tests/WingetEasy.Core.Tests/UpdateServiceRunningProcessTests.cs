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
using Xunit;

namespace WingetEasy.Core.Tests;

public class UpdateServiceRunningProcessTests
{
    private readonly Mock<IWingetService> _wingetService = new();
    private readonly Mock<IUpdateHistoryRepository> _historyRepo = new();
    private readonly Mock<IAdminElevationHelper> _adminHelper = new();
    private readonly Mock<IRunningProcessService> _runningProcessService = new();

    private UpdateService CreateService()
    {
        return new UpdateService(
            _wingetService.Object,
            _historyRepo.Object,
            _adminHelper.Object,
            _runningProcessService.Object);
    }

    [Fact]
    public async Task AutoUpdate_WhenAppHasVisibleWindow_ShouldSkipAutomaticallyWithoutPrompt()
    {
        // Arrange
        const string pkgId = "Discord.Discord";
        _runningProcessService.Setup(r => r.IsAppRunningWithVisibleWindow(pkgId, null))
            .Returns(true);

        var service = CreateService();

        // Act
        var results = await service.ExecuteUpdatesAsync(
            [pkgId],
            progress: null,
            confirmElevation: null,
            handleRunningApp: null,
            isAutoUpdate: true);

        // Assert
        results.Should().HaveCount(1);
        results[0].Skipped.Should().BeTrue();
        results[0].IsApplicationRunning.Should().BeTrue();
        results[0].Success.Should().BeFalse();

        // Should NOT invoke winget install
        _wingetService.Verify(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        // Should NOT record failure in update history
        _historyRepo.Verify(h => h.AddAsync(It.IsAny<UpdateResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AutoUpdate_WhenAppHasNoVisibleWindow_ShouldProceedWithUpdate()
    {
        // Arrange
        const string pkgId = "Google.Chrome";
        _runningProcessService.Setup(r => r.IsAppRunningWithVisibleWindow(pkgId, null))
            .Returns(false);

        _wingetService.Setup(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult(pkgId, "Chrome", true));

        var service = CreateService();

        // Act
        var results = await service.ExecuteUpdatesAsync(
            [pkgId],
            progress: null,
            confirmElevation: null,
            handleRunningApp: null,
            isAutoUpdate: true);

        // Assert
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        results[0].Skipped.Should().BeFalse();
        _wingetService.Verify(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AutoUpdate_WhenWingetReturnsCode0x8A150056_ShouldSkipAutomatically()
    {
        // Arrange
        const string pkgId = "Microsoft.VisualStudioCode";
        _runningProcessService.Setup(r => r.IsAppRunningWithVisibleWindow(pkgId, null))
            .Returns(false);

        // Winget returns 0x8A150056 (-1978335146)
        _wingetService.Setup(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult(
                pkgId,
                "VS Code",
                false,
                "Application is currently running",
                ExitCode: WingetExitCodes.AppRunning,
                IsApplicationRunning: true));

        var service = CreateService();

        // Act
        var results = await service.ExecuteUpdatesAsync(
            [pkgId],
            progress: null,
            confirmElevation: null,
            handleRunningApp: null,
            isAutoUpdate: true);

        // Assert
        results.Should().HaveCount(1);
        results[0].Skipped.Should().BeTrue();
        results[0].IsApplicationRunning.Should().BeTrue();
        _historyRepo.Verify(h => h.AddAsync(It.IsAny<UpdateResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Interactive_WhenAppIsRunning_AndUserChoosesSkip_ShouldSkipUpdate()
    {
        // Arrange
        const string pkgId = "Discord.Discord";
        _runningProcessService.Setup(r => r.IsAppRunning(pkgId, null))
            .Returns(true);

        bool promptCalled = false;
        Func<string, Task<AppRunningResolution>> prompt = appName =>
        {
            promptCalled = true;
            return Task.FromResult(AppRunningResolution.Skip);
        };

        var service = CreateService();

        // Act
        var results = await service.ExecuteUpdatesAsync(
            [pkgId],
            progress: null,
            confirmElevation: null,
            handleRunningApp: prompt,
            isAutoUpdate: false);

        // Assert
        promptCalled.Should().BeTrue();
        results.Should().HaveCount(1);
        results[0].Skipped.Should().BeTrue();
        results[0].IsApplicationRunning.Should().BeTrue();

        _wingetService.Verify(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Interactive_WhenAppIsRunning_AndUserChoosesCloseAndUpdate_ShouldCloseProcessAndInstall()
    {
        // Arrange
        const string pkgId = "Discord.Discord";
        _runningProcessService.Setup(r => r.IsAppRunning(pkgId, null))
            .Returns(true);
        _runningProcessService.Setup(r => r.CloseAppProcessesAsync(pkgId, null, true, null))
            .ReturnsAsync(true);

        _wingetService.Setup(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult(pkgId, "Discord", true));

        bool promptCalled = false;
        Func<string, Task<AppRunningResolution>> prompt = appName =>
        {
            promptCalled = true;
            return Task.FromResult(AppRunningResolution.CloseAndUpdate);
        };

        var service = CreateService();

        // Act
        var results = await service.ExecuteUpdatesAsync(
            [pkgId],
            progress: null,
            confirmElevation: null,
            handleRunningApp: prompt,
            isAutoUpdate: false);

        // Assert
        promptCalled.Should().BeTrue();
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        results[0].Skipped.Should().BeFalse();

        _runningProcessService.Verify(r => r.CloseAppProcessesAsync(pkgId, null, true, null), Times.Once);
        _wingetService.Verify(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Interactive_WhenWingetReturnsCode0x8A150056_AndUserChoosesCloseAndUpdate_ShouldRetryAndSucceed()
    {
        // Arrange
        const string pkgId = "Obsidian.Obsidian";
        _runningProcessService.Setup(r => r.IsAppRunning(pkgId, null))
            .Returns(false); // Initially not caught

        var firstAttempt = new UpdateResult(
            pkgId,
            "Obsidian",
            false,
            "Application is currently running",
            ExitCode: WingetExitCodes.AppRunning,
            IsApplicationRunning: true);

        var secondAttempt = new UpdateResult(pkgId, "Obsidian", true);

        var callCount = 0;
        _wingetService.Setup(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? firstAttempt : secondAttempt);

        _runningProcessService.Setup(r => r.CloseAppProcessesAsync(pkgId, null, true, null))
            .ReturnsAsync(true);

        bool promptCalled = false;
        Func<string, Task<AppRunningResolution>> prompt = appName =>
        {
            promptCalled = true;
            return Task.FromResult(AppRunningResolution.CloseAndUpdate);
        };

        var service = CreateService();

        // Act
        var results = await service.ExecuteUpdatesAsync(
            [pkgId],
            progress: null,
            confirmElevation: null,
            handleRunningApp: prompt,
            isAutoUpdate: false);

        // Assert
        promptCalled.Should().BeTrue();
        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        _runningProcessService.Verify(r => r.CloseAppProcessesAsync(pkgId, null, true, null), Times.Once);
        _wingetService.Verify(w => w.InstallUpdateAsync(pkgId, It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

