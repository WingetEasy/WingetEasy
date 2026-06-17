using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite; // <-- ADICIONADO
using WingetEasy.Data;
using Xunit;

namespace WingetEasy.Core.Tests;

public class DatabaseMigrationTests
{
    [Fact]
    public async Task MigrateAsync_AppliesSchemaWithoutErrors()
    {
        var dbPath = Path.GetTempFileName();
        try
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using var db = new AppDbContext(opts);

            var act = async () => await db.Database.MigrateAsync().ConfigureAwait(false);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            // Liberta o ficheiro antes de apagar
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
