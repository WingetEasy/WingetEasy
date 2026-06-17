using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite; // <-- ADICIONADO
using WingetEasy.Data;

namespace WingetEasy.Core.Tests;

public abstract class RepositoryTestBase : IDisposable
{
    protected readonly AppDbContext Db;
    private readonly string _dbPath = Path.GetTempFileName();

    protected RepositoryTestBase()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        Db = new AppDbContext(opts);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Database.CloseConnection(); // Garante que a conexão do teste fechou
        Db.Dispose();

        // MÁGICA: Limpa a memória do driver SQLite e solta o "lock" do ficheiro
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            // Agora o Windows deixa apagar!
            File.Delete(_dbPath);
        }

        GC.SuppressFinalize(this);
    }
}
