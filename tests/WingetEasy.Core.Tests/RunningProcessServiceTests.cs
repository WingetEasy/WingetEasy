using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using WingetEasy.Core.Services;
using Xunit;

namespace WingetEasy.Core.Tests;

public class RunningProcessServiceTests
{
    [Theory]
    [InlineData("Discord.Discord", "Discord")]
    [InlineData("Google.Chrome", "chrome")]
    [InlineData("Microsoft.VisualStudioCode", "Code")]
    [InlineData("Microsoft.Teams", "Teams")]
    [InlineData("Zoom.Zoom", "Zoom")]
    [InlineData("Obsidian.Obsidian", "Obsidian")]
    [InlineData("Spotify.Spotify", "Spotify")]
    [InlineData("Valve.Steam", "steam")]
    [InlineData("Notepad++.Notepad++", "notepad++")]
    [InlineData("VideoLAN.VLC", "vlc")]
    [InlineData("Brave.Brave", "brave")]
    [InlineData("Microsoft.Edge", "msedge")]
    public void IsAppRunning_WithKnownProcessMap_ShouldDetectRunningApp(string packageId, string processName)
    {
        var snapshot = new ProcessSnapshot(
            id: 1234,
            processName: processName,
            mainWindowTitle: "Main App Window",
            mainWindowHandle: new IntPtr(100),
            hasVisibleWindow: true,
            hasExitedFunc: () => false);

        var service = new RunningProcessService(() => [snapshot]);

        var isRunning = service.IsAppRunning(packageId);

        isRunning.Should().BeTrue();
    }

    [Fact]
    public void IsAppRunning_WhenProcessHasExited_ShouldReturnFalse()
    {
        var snapshot = new ProcessSnapshot(
            id: 1234,
            processName: "Discord",
            mainWindowTitle: "Discord",
            mainWindowHandle: new IntPtr(100),
            hasVisibleWindow: true,
            hasExitedFunc: () => true); // Has exited

        var service = new RunningProcessService(() => [snapshot]);

        var isRunning = service.IsAppRunning("Discord.Discord");

        isRunning.Should().BeFalse();
    }

    [Fact]
    public void IsAppRunningWithVisibleWindow_WhenProcessHasVisibleWindow_ShouldReturnTrue()
    {
        var snapshot = new ProcessSnapshot(
            id: 1234,
            processName: "chrome",
            mainWindowTitle: "Google Chrome - New Tab",
            mainWindowHandle: new IntPtr(100),
            hasVisibleWindow: true,
            hasExitedFunc: () => false);

        var service = new RunningProcessService(() => [snapshot]);

        var result = service.IsAppRunningWithVisibleWindow("Google.Chrome");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAppRunningWithVisibleWindow_WhenProcessHasNoVisibleWindow_ShouldReturnFalse()
    {
        var snapshot = new ProcessSnapshot(
            id: 1234,
            processName: "chrome",
            mainWindowTitle: "",
            mainWindowHandle: IntPtr.Zero,
            hasVisibleWindow: false,
            hasExitedFunc: () => false);

        var service = new RunningProcessService(() => [snapshot]);

        var isRunning = service.IsAppRunning("Google.Chrome");
        var hasVisibleWindow = service.IsAppRunningWithVisibleWindow("Google.Chrome");

        isRunning.Should().BeTrue();
        hasVisibleWindow.Should().BeFalse();
    }

    [Fact]
    public void IsAppRunning_WithUnmappedPackage_ShouldUseHeuristicFromPackageIdAndName()
    {
        var snapshot = new ProcessSnapshot(
            id: 5678,
            processName: "SampleEditor",
            mainWindowTitle: "Sample Editor",
            mainWindowHandle: new IntPtr(200),
            hasVisibleWindow: true,
            hasExitedFunc: () => false);

        var service = new RunningProcessService(() => [snapshot]);

        var runningById = service.IsAppRunning("Vendor.SampleEditor");
        var runningByName = service.IsAppRunning("Vendor.Other", "Sample Editor");

        runningById.Should().BeTrue();
        runningByName.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAppProcessesAsync_GracefulClose_ShouldInvokeCloseMainWindow()
    {
        bool closeCalled = false;
        bool exited = false;

        var snapshot = new ProcessSnapshot(
            id: 1234,
            processName: "Discord",
            mainWindowTitle: "Discord",
            mainWindowHandle: new IntPtr(100),
            hasVisibleWindow: true,
            hasExitedFunc: () => exited,
            closeMainWindowAction: () =>
            {
                closeCalled = true;
                exited = true;
            });

        var service = new RunningProcessService(() => [snapshot]);

        var result = await service.CloseAppProcessesAsync("Discord.Discord", timeout: TimeSpan.FromMilliseconds(500));

        closeCalled.Should().BeTrue();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAppProcessesAsync_Unresponsive_ShouldFallbackToKill()
    {
        bool killCalled = false;
        bool exited = false;

        var snapshot = new ProcessSnapshot(
            id: 1234,
            processName: "Discord",
            mainWindowTitle: "Discord",
            mainWindowHandle: new IntPtr(100),
            hasVisibleWindow: true,
            hasExitedFunc: () => exited,
            closeMainWindowAction: () =>
            {
                // Unresponsive: does not exit
            },
            killAction: () =>
            {
                killCalled = true;
                exited = true;
            });

        var service = new RunningProcessService(() => [snapshot]);

        var result = await service.CloseAppProcessesAsync("Discord.Discord", killIfUnresponsive: true, timeout: TimeSpan.FromMilliseconds(300));

        killCalled.Should().BeTrue();
        result.Should().BeTrue();
    }
}

