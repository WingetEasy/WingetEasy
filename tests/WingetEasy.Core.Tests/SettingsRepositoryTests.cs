using System.Threading.Tasks;
using FluentAssertions;
using WingetEasy.Data.Repositories;
using Xunit;

namespace WingetEasy.Core.Tests;

public class SettingsRepositoryTests : RepositoryTestBase
{
    private readonly SettingsRepository _repository;

    public SettingsRepositoryTests()
    {
        _repository = new SettingsRepository(Db);
    }

    private record DummyConfig(string Name, int Version);

    [Fact]
    public async Task GetSetString_WorksCorrectly()
    {
        await _repository.SetAsync("TestKey", "TestValue");

        // REMOVIDO o <string> daqui para ele usar o método nativo de texto
        var result = await _repository.GetAsync("TestKey");

        result.Should().Be("TestValue");
    }

    [Fact]
    public async Task GetSetJson_WorksCorrectly()
    {
        var obj = new DummyConfig("WingetEasy", 1);
        await _repository.SetAsync("TestObj", obj);

        var result = await _repository.GetAsync<DummyConfig>("TestObj");

        result.Should().NotBeNull();
        result!.Name.Should().Be("WingetEasy");
        result.Version.Should().Be(1);
    }

    [Fact]
    public async Task Get_NonExistentKey_ReturnsNull()
    {
        var result = await _repository.GetAsync<string>("MissingKey");
        result.Should().BeNull();
    }
}
