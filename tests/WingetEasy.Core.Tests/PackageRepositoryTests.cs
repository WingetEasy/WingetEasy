using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using WingetEasy.Data.Entities;
using WingetEasy.Data.Repositories;
using Xunit;

namespace WingetEasy.Core.Tests;

public class PackageRepositoryTests : RepositoryTestBase
{
    private readonly PackageRepository _repository;

    public PackageRepositoryTests()
    {
        _repository = new PackageRepository(Db);
    }

    [Fact]
    public async Task SkipAndUnskip_WorksCorrectly()
    {
        // Agora usamos apenas o ID, Razão (opcional) e o CancellationToken
        await _repository.SkipPackageAsync("App.Test", "Motivo de teste", CancellationToken.None);

        var skipped = await _repository.GetSkippedIdsAsync(CancellationToken.None);
        skipped.Should().Contain("App.Test");

        await _repository.UnskipPackageAsync("App.Test", CancellationToken.None);

        skipped = await _repository.GetSkippedIdsAsync(CancellationToken.None);
        skipped.Should().NotContain("App.Test");
    }

    [Fact]
    public async Task GetSkippedIds_ReturnsOnlySkipped()
    {
        await _repository.SkipPackageAsync("App.Skipped", "Ignorado", CancellationToken.None);

        // Criando a entidade com todas as propriedades 'required'
        Db.Packages.Add(new PackageEntity {
            WingetId = "App.NotSkipped",
            Name = "Not Skipped App",
            Source = "winget",
            IsSkipped = false
        });
        await Db.SaveChangesAsync();

        var skipped = await _repository.GetSkippedIdsAsync(CancellationToken.None);

        skipped.Should().HaveCount(1);
        skipped.Should().Contain("App.Skipped");
        skipped.Should().NotContain("App.NotSkipped");
    }
}
