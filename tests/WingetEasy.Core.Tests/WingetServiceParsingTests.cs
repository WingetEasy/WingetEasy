using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Services;
using Xunit;

namespace WingetEasy.Core.Tests;

public class WingetServiceParsingTests
{
    private readonly WingetService _wingetService;

    public WingetServiceParsingTests()
    {
        // Mockamos as dependências (Banco de Dados e Log) para testar apenas o WingetService puro
        var repoMock = new Mock<IPackageRepository>();
        var loggerMock = new Mock<ILogger<WingetService>>();

        _wingetService = new WingetService(repoMock.Object, loggerMock.Object);
    }

    [Fact]
    public void ParseWingetJson_WithValidOutput_ReturnsPackages()
    {
        // Arrange
        var json = File.ReadAllText("TestData/winget_upgrade_valid.json");

        // Act
        var result = _wingetService.ParseWingetJsonInternal(json).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.First().Id.Should().Be("Microsoft.VSCode");
        result.First().AvailableVersion.Should().Be("1.83.0");
    }

    [Fact]
    public void ParseWingetJson_WithEmptyOutput_ReturnsEmptyList()
    {
        var json = File.ReadAllText("TestData/winget_upgrade_empty.json");
        var result = _wingetService.ParseWingetJsonInternal(json);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseWingetJson_WithMixedOutput_IgnoresGarbageAndReturnsPackages()
    {
        // Winget antigo jogava barras de progresso antes do JSON. O parser tem que sobreviver a isso.
        var json = File.ReadAllText("TestData/winget_upgrade_mixed.json");
        var result = _wingetService.ParseWingetJsonInternal(json).ToList();

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("Mozilla.Firefox");
    }

    [Fact]
    public void ParseWingetJson_WithMalformedJson_GracefullyFailsAndReturnsEmptyList()
    {
        var json = File.ReadAllText("TestData/winget_upgrade_malformed.json");
        var result = _wingetService.ParseWingetJsonInternal(json);
        result.Should().BeEmpty(); // Não deve estoirar a aplicação
    }

    [Fact]
    public void ParseWingetTable_WithPtBrRealOutputAndProgressSpinners_ReturnsAllPackages()
    {
        var ptBrOutput =
            "   - \r   \\ \r   | \r   / \r" +
            "Nome                            Id                               Versão          Disponível    Origem\r\n" +
            "-----------------------------------------------------------------------------------------------------\r\n" +
            "7-Zip 26.02 (x64)               7zip.7zip                        26.02           26.04         winget\r\n" +
            "Microsoft Visual Studio Code    Microsoft.VSCode                 1.89.1          1.90.0        winget\r\n" +
            "Notepad++ (64-bit x64)          Notepad++.Notepad++              8.9.7           8.9.8.1       winget\r\n" +
            "PowerToys (Preview) x64         Microsoft.PowerToys              0.100.2         0.101.2362.0  winget\r\n" +
            "Ubisoft Connect                 Ubisoft.Connect                  < 174.2.0.13389 174.2.0.13389 winget\r\n" +
            "5 atualizações disponíveis.\r\n" +
            "\r\n" +
            "Os pacotes a seguir têm uma atualização disponível, mas exigem uma segmentação explícita para atualização:\r\n" +
            "Nome    Id              Versão   Disponível Origem\r\n" +
            "--------------------------------------------------\r\n" +
            "Discord Discord.Discord 1.0.9255 1.0.9264   winget\r\n";

        var result = _wingetService.ParseWingetTableInternal(ptBrOutput).ToList();

        result.Should().HaveCount(5);

        result[0].Name.Should().Be("7-Zip 26.02 (x64)");
        result[0].Id.Should().Be("7zip.7zip");
        result[0].CurrentVersion.Should().Be("26.02");
        result[0].AvailableVersion.Should().Be("26.04");
        result[0].Source.Should().Be("winget");

        result[1].Name.Should().Be("Microsoft Visual Studio Code");
        result[1].Id.Should().Be("Microsoft.VSCode");
        result[1].CurrentVersion.Should().Be("1.89.1");
        result[1].AvailableVersion.Should().Be("1.90.0");

        result[2].Id.Should().Be("Notepad++.Notepad++");
        result[2].AvailableVersion.Should().Be("8.9.8.1");

        result[4].Name.Should().Be("Ubisoft Connect");
        result[4].Id.Should().Be("Ubisoft.Connect");
        result[4].CurrentVersion.Should().Be("< 174.2.0.13389");
        result[4].AvailableVersion.Should().Be("174.2.0.13389");
    }

    [Fact]
    public async Task CheckUpdatesAsync_WithPtBrWingetOutput_ExecutesCorrectArgsAndReturnsPackages()
    {
        string? capturedArgs = null;
        var ptBrOutput =
            "   - \r   \\ \r   | \r   / \r" +
            "Nome                  Id                    Versão   Disponível Origem\r\n" +
            "----------------------------------------------------------------------\r\n" +
            "Visual Studio Code    Microsoft.VSCode      1.89.1   1.90.0     winget\r\n" +
            "Google Chrome         Google.Chrome         124.0.6  125.0.6    winget\r\n" +
            "2 atualizações disponíveis.\r\n";

        var repoMock = new Mock<IPackageRepository>();
        repoMock.Setup(r => r.GetSkippedIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var loggerMock = new Mock<ILogger<WingetService>>();

        var service = new WingetService(
            repoMock.Object,
            loggerMock.Object,
            (args, _) =>
            {
                capturedArgs = args;
                return Task.FromResult((0, ptBrOutput, string.Empty));
            });

        var packages = (await service.CheckUpdatesAsync()).ToList();

        capturedArgs.Should().Be("upgrade --accept-source-agreements --disable-interactivity");
        packages.Should().HaveCount(2);
        packages[0].Id.Should().Be("Microsoft.VSCode");
        packages[1].Id.Should().Be("Google.Chrome");
    }

    [Theory]
    [InlineData("Invalid ID!")]
    [InlineData("pkg; rm -rf /")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ValidatePackageId_WithInvalidInput_ThrowsArgumentException(string? invalidId)
    {
        // Assert: Ações fluentes que verificam se uma exceção é lançada
        FluentActions.Invoking(() => WingetService.ValidatePackageId(invalidId!))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidatePackageId_WithValidInput_DoesNotThrow()
    {
        FluentActions.Invoking(() => WingetService.ValidatePackageId("Microsoft.VSCode"))
            .Should().NotThrow();
    }

    [Fact]
    public async Task RepairSourcesAsync_WhenResetAndUpdateSucceed_ExecutesCommandsInOrderAndReturnsTrue()
    {
        var executedCommands = new System.Collections.Generic.List<string>();
        var repoMock = new Mock<IPackageRepository>();
        var loggerMock = new Mock<ILogger<WingetService>>();

        var service = new WingetService(
            repoMock.Object,
            loggerMock.Object,
            (args, _) =>
            {
                executedCommands.Add(args);
                return Task.FromResult((0, "Done", string.Empty));
            });

        var result = await service.RepairSourcesAsync();

        result.Should().BeTrue();
        executedCommands.Should().ContainInOrder("source reset --force", "source update");
        executedCommands.Should().HaveCount(2);
    }

    [Fact]
    public async Task RepairSourcesAsync_WhenNotElevatedAndResetFailsByPermission_UsesElevationAndReturnsTrue()
    {
        var executedCommands = new System.Collections.Generic.List<string>();
        var elevatedCalled = false;
        var repoMock = new Mock<IPackageRepository>();
        var loggerMock = new Mock<ILogger<WingetService>>();
        var adminMock = new Mock<IAdminElevationHelper>();
        adminMock.Setup(a => a.IsRunningAsAdmin()).Returns(false);

        var service = new WingetService(
            repoMock.Object,
            loggerMock.Object,
            (args, _) =>
            {
                executedCommands.Add(args);
                return Task.FromResult((5, string.Empty, "Access is denied. Administrator privileges required."));
            },
            adminMock.Object,
            _ =>
            {
                elevatedCalled = true;
                return Task.FromResult(true);
            });

        var result = await service.RepairSourcesAsync();

        result.Should().BeTrue();
        elevatedCalled.Should().BeTrue();
        executedCommands.Should().ContainSingle().Which.Should().Be("source reset --force");
    }

    [Fact]
    public async Task RepairSourcesAsync_WhenPermissionFailsAndElevationDeclined_ReturnsFalseGracefully()
    {
        var repoMock = new Mock<IPackageRepository>();
        var loggerMock = new Mock<ILogger<WingetService>>();
        var adminMock = new Mock<IAdminElevationHelper>();
        adminMock.Setup(a => a.IsRunningAsAdmin()).Returns(false);

        var service = new WingetService(
            repoMock.Object,
            loggerMock.Object,
            (_, _) => Task.FromResult((unchecked((int)0x80070005), string.Empty, "Acesso negado.")),
            adminMock.Object,
            _ => Task.FromResult(false));

        var result = await service.RepairSourcesAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RepairSourcesAsync_WhenSourceUpdateFails_ReturnsFalse()
    {
        var repoMock = new Mock<IPackageRepository>();
        var loggerMock = new Mock<ILogger<WingetService>>();

        var service = new WingetService(
            repoMock.Object,
            loggerMock.Object,
            (args, _) =>
            {
                var exitCode = args == "source reset --force" ? 0 : 1;
                return Task.FromResult((exitCode, string.Empty, exitCode == 0 ? string.Empty : "Failed to update source"));
            });

        var result = await service.RepairSourcesAsync();

        result.Should().BeFalse();
    }
}
