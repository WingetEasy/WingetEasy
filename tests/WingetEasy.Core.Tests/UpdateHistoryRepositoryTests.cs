using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using WingetEasy.Core.Models;
using WingetEasy.Data.Entities;
using WingetEasy.Data.Repositories;
using Xunit;

namespace WingetEasy.Core.Tests;

public class UpdateHistoryRepositoryTests : RepositoryTestBase
{
    private readonly UpdateHistoryRepository _repository;

    public UpdateHistoryRepositoryTests()
    {
        _repository = new UpdateHistoryRepository(Db);
    }

    [Fact]
    public async Task AddAsync_SavesSuccessfully()
    {
        var result = new UpdateResult("Microsoft.VSCode", "Visual Studio Code", true, null, TimeSpan.FromSeconds(5));

        await _repository.AddAsync(result);

        var history = await _repository.GetRecentAsync(10);
        history.Should().HaveCount(1);
        history.First().PackageId.Should().Be("Microsoft.VSCode");
    }

    [Fact]
    public async Task GetRecentAsync_OrdersByDateDesc()
    {
        // Preenchendo a entidade real (usando UpdatedAt)
        Db.UpdateHistories.Add(new UpdateHistoryEntity {
            PackageId = "Old",
            PackageName = "Old App",
            FromVersion = "1.0",
            ToVersion = "2.0",
            UpdatedAt = DateTime.UtcNow.AddDays(-2), // Corrigido de Date para UpdatedAt
            Status = UpdateStatus.Success
        });

        Db.UpdateHistories.Add(new UpdateHistoryEntity {
            PackageId = "New",
            PackageName = "New App",
            FromVersion = "1.0",
            ToVersion = "2.0",
            UpdatedAt = DateTime.UtcNow, // Corrigido de Date para UpdatedAt
            Status = UpdateStatus.Success
        });

        await Db.SaveChangesAsync();

        var history = await _repository.GetRecentAsync(10);
        history.Should().HaveCount(2);
        history.First().PackageId.Should().Be("New"); // O mais recente deve vir primeiro
    }
}
