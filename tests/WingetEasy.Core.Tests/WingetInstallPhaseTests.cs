using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Models;
using WingetEasy.Core.Services;
using Xunit;

namespace WingetEasy.Core.Tests;

public class WingetInstallPhaseTests
{
    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    [Theory]
    [InlineData("Downloading https://example.com/setup.exe", "Baixando instalador...")]
    [InlineData("Baixando o instalador...", "Baixando instalador...")]
    [InlineData("  25.4 MB / 100 MB", "Baixando instalador...")]
    [InlineData("Verifying installer hash...", "Verificando integridade (hash)...")]
    [InlineData("Verificando hash do instalador...", "Verificando integridade (hash)...")]
    [InlineData("Validating SHA256...", "Verificando integridade (hash)...")]
    [InlineData("Starting package install...", "Instalando pacote...")]
    [InlineData("Iniciando a instalação do pacote...", "Instalando pacote...")]
    [InlineData("Installing LibreOffice...", "Instalando pacote...")]
    [InlineData("Instalando o pacote selecionado...", "Instalando pacote...")]
    public void DetectInstallPhase_IdentifiesKnownPhasesCorrectly(string line, string expectedPhase)
    {
        var phase = WingetService.DetectInstallPhase(line);
        phase.Should().Be(expectedPhase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Found Google Chrome [Google.Chrome]")]
    [InlineData("Successfully installed")]
    public void DetectInstallPhase_ReturnsNullForUnrelatedLines(string line)
    {
        var phase = WingetService.DetectInstallPhase(line);
        phase.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteUpdatesAsync_ForwardsPhaseProgressCorrectly()
    {
        const string packageId = "7zip.7zip";
        var wingetService = new Mock<IWingetService>();
        var historyRepo = new Mock<IUpdateHistoryRepository>();
        var elevationHelper = new Mock<IAdminElevationHelper>();

        wingetService.Setup(w => w.InstallUpdateAsync(
                packageId,
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<string>?>()))
            .Callback<string, IProgress<int>?, CancellationToken, IProgress<string>?>((id, p, ct, phase) =>
            {
                phase?.Report("Baixando instalador...");
                phase?.Report("Verificando integridade (hash)...");
                phase?.Report("Instalando pacote...");
            })
            .ReturnsAsync(new UpdateResult(packageId, "7-Zip", true));

        var updateService = new UpdateService(wingetService.Object, historyRepo.Object, elevationHelper.Object);
        var reportedPhases = new List<(string packageId, string phase)>();
        var phaseProgress = new SynchronousProgress<(string packageId, string phase)>(tuple => reportedPhases.Add(tuple));

        var results = await updateService.ExecuteUpdatesAsync(
            [packageId],
            progress: null,
            confirmElevation: null,
            handleRunningApp: null,
            isAutoUpdate: false,
            ct: CancellationToken.None,
            phaseProgress: phaseProgress);

        results.Should().ContainSingle().Which.Success.Should().BeTrue();
        reportedPhases.Should().Contain(p => p.packageId == packageId && p.phase == "Baixando instalador...");
        reportedPhases.Should().Contain(p => p.packageId == packageId && p.phase == "Verificando integridade (hash)...");
        reportedPhases.Should().Contain(p => p.packageId == packageId && p.phase == "Instalando pacote...");
    }
}

