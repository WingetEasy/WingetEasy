using System;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace WingetEasy.Data;

/// <summary>
/// Contexto de dados principal da aplicação utilizando SQLite.
/// Gerencia a conexão com a base de dados local física.
/// </summary>

public class AppDbContext : DbContext
{
    /// <summary>
    /// Construtor vazio padrão, exigido pelas ferramentas de Design do EF Core (Migrations).
    /// </summary>

    public AppDbContext() { }

    /// <summary>
    /// Construtor utilizado pelo contêiner de Injeção de Dependência (DI).
    /// </summary>
    /// <param name="options">Opções de configuração do contexto.</param>


    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Configura a conexão com o banco de dados físico de forma dinâmica,
    /// apontando para a pasta %APPDATA%/WingetEasy/data.db.
    /// </summary>

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if(!optionsBuilder.IsConfigured)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "WingetEasy");

            Directory.CreateDirectory(folder); // Garante que a pasta exista

            var dbPath = Path.Combine(folder, "data.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

}
