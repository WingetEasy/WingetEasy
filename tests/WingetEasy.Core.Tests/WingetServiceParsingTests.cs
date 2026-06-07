using System;
using System.IO;
using System.Linq;
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

    [Theory]
    [InlineData("Invalid ID!")]
    [InlineData("pkg; rm -rf /")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ValidatePackageId_WithInvalidInput_ThrowsArgumentException(string invalidId)
    {
        // Assert: Ações fluentes que verificam se uma exceção é lançada
        FluentActions.Invoking(() => WingetService.ValidatePackageId(invalidId))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidatePackageId_WithValidInput_DoesNotThrow()
    {
        FluentActions.Invoking(() => WingetService.ValidatePackageId("Microsoft.VSCode"))
            .Should().NotThrow();
    }
}
