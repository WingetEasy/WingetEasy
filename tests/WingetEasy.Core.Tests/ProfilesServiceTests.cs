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

public sealed class ProfilesServiceTests
{
    [Fact]
    public async Task GetAllAsync_SeedsProfilesWhenRepositoryIsEmpty()
    {
        var repository = new Mock<IProfileRepository>();
        repository.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());
        var service = CreateService(repository);

        var profiles = await service.GetAllAsync();

        profiles.Should().HaveCount(2);
        profiles.Should().Contain(profile => profile.Name == "Desenvolvimento");
        profiles.Should().Contain(profile => profile.Name == "Utilitários");
        repository.Verify(item => item.SaveAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SaveAndDeleteAsync_DelegateCompleteCrudToRepository()
    {
        var repository = new Mock<IProfileRepository>();
        repository.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Profile>());
        var service = CreateService(repository);
        var profile = new Profile(
            Guid.Empty,
            "Trabalho",
            "Ferramentas",
            DateTime.UtcNow,
            [new ProfilePackage("Contoso.App", "Contoso")]);

        var saved = await service.SaveAsync(profile);
        await service.DeleteAsync(saved.Id);

        saved.Id.Should().NotBe(Guid.Empty);
        repository.Verify(item => item.SaveAsync(
            It.Is<Profile>(value => value.Name == "Trabalho" && value.Packages.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.DeleteAsync(saved.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallProfileAsync_InstallsPackagesReportsProgressAndStoresHistory()
    {
        var profile = new Profile(
            Guid.NewGuid(),
            "Teste",
            "Perfil de teste",
            DateTime.UtcNow,
            [
                new ProfilePackage("Contoso.One", "One"),
                new ProfilePackage("Contoso.Two", "Two")
            ]);
        var repository = new Mock<IProfileRepository>();
        repository.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        var winget = new Mock<IWingetService>();
        winget.Setup(item => item.InstallUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, IProgress<int>? _, CancellationToken _) =>
                new UpdateResult(id, id, true));
        var history = new Mock<IUpdateHistoryRepository>();
        var service = new ProfilesService(repository.Object, winget.Object, history.Object);
        var progressValues = new List<(string packageId, int current, int total)>();

        var results = await service.InstallProfileAsync(
            profile.Id,
            new InlineProgress<(string packageId, int current, int total)>(progressValues.Add));

        results.Should().HaveCount(2).And.OnlyContain(result => result.Success);
        progressValues.Select(value => value.current).Should().Equal(1, 2);
        history.Verify(item => item.AddAsync(It.IsAny<UpdateResult>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static ProfilesService CreateService(Mock<IProfileRepository> repository) =>
        new(
            repository.Object,
            Mock.Of<IWingetService>(),
            Mock.Of<IUpdateHistoryRepository>());

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
