using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Core.Services;

namespace WingetEasy.Core.Tests;

public sealed class WindowsTaskSchedulerServiceTests
{
    [Fact]
    public void BuildCreateArguments_ShouldCreateDailyCheckTask()
    {
        var arguments = WindowsTaskSchedulerService.BuildCreateArguments(
            new ScheduleConfig(ScheduleFrequency.OnceDaily, new TimeSpan(8, 30, 0)),
            @"C:\Apps\WingetEasy.exe");

        arguments.Should().Contain("/Create");
        arguments.Should().Contain("/TN \"WingetEasy\\AutoCheck\"");
        arguments.Should().Contain("/TR \"\\\"C:\\Apps\\WingetEasy.exe\\\" --check\"");
        arguments.Should().Contain("/SC DAILY /ST 08:30");
    }

    [Fact]
    public void BuildCreateArguments_ShouldCreateHourlyAndWeeklySchedules()
    {
        var hourly = WindowsTaskSchedulerService.BuildCreateArguments(
            new ScheduleConfig(ScheduleFrequency.TwiceDaily, TimeSpan.Zero),
            @"C:\Apps\WingetEasy.exe");
        var weekly = WindowsTaskSchedulerService.BuildCreateArguments(
            new ScheduleConfig(ScheduleFrequency.Weekly, new TimeSpan(21, 15, 0), [1]),
            @"C:\Apps\WingetEasy.exe");

        hourly.Should().Contain("/SC HOURLY /MO 12");
        weekly.Should().Contain("/SC WEEKLY /D MON /ST 21:15");
    }

    [Fact]
    public async Task CreateOrUpdateTaskAsync_ShouldRunCreateCommand()
    {
        var runner = new Mock<ITaskSchedulerProcessRunner>();
        runner.Setup(item => item.RunAsync(
                "schtasks.exe",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var service = new WindowsTaskSchedulerService(
            runner.Object,
            Mock.Of<ILogger<WindowsTaskSchedulerService>>(),
            @"C:\Apps\WingetEasy.exe");

        await service.CreateOrUpdateTaskAsync(
            new ScheduleConfig(ScheduleFrequency.OnceDaily, new TimeSpan(7, 0, 0)));

        runner.Verify(item => item.RunAsync(
            "schtasks.exe",
            It.Is<string>(args => args.Contains("/Create") && args.Contains("/SC DAILY /ST 07:00")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateTaskAsync_Manual_ShouldDeleteTask()
    {
        var runner = new Mock<ITaskSchedulerProcessRunner>();
        runner.Setup(item => item.RunAsync(
                "schtasks.exe",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var service = new WindowsTaskSchedulerService(
            runner.Object,
            Mock.Of<ILogger<WindowsTaskSchedulerService>>(),
            @"C:\Apps\WingetEasy.exe");

        await service.CreateOrUpdateTaskAsync(
            new ScheduleConfig(ScheduleFrequency.Manual, TimeSpan.Zero));

        runner.Verify(item => item.RunAsync(
            "schtasks.exe",
            It.Is<string>(args => args.Contains("/Delete") && args.Contains("/F")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TaskExistsAsync_ShouldReflectSchtasksExitCode()
    {
        var runner = new Mock<ITaskSchedulerProcessRunner>();
        runner.Setup(item => item.RunAsync(
                "schtasks.exe",
                It.Is<string>(args => args.Contains("/Query")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var service = new WindowsTaskSchedulerService(
            runner.Object,
            Mock.Of<ILogger<WindowsTaskSchedulerService>>(),
            @"C:\Apps\WingetEasy.exe");

        var exists = await service.TaskExistsAsync();

        exists.Should().BeTrue();
    }
}
