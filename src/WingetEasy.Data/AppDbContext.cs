using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WingetEasy.Data.Entities;

namespace WingetEasy.Data;

/// <summary>
/// Contexto de dados principal da aplicação utilizando SQLite.
/// Gerencia a conexão com a base de dados local física.
/// </summary>

public class AppDbContext : DbContext
{

    public DbSet<PackageEntity> Packages => Set<PackageEntity>();
    public DbSet<UpdateHistoryEntity> UpdateHistories => Set<UpdateHistoryEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();
    public DbSet<CheckLogEntity> CheckLogs => Set<CheckLogEntity>();
    public DbSet<ProfileEntity> Profiles => Set<ProfileEntity>();
    public DbSet<BackupSnapshotEntity> BackupSnapshots => Set<BackupSnapshotEntity>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Chave primária da SettingEntity (que é uma string 'Key')
        modelBuilder.Entity<SettingEntity>()
            .HasKey(s => s.Key);

        // Índice único para a PackageEntity (WingetId não pode se repetir)
        modelBuilder.Entity<PackageEntity>()
            .HasIndex(p => p.WingetId)
            .IsUnique();

        // Índice ordenado decrescente em UpdatedAt para melhorar performance de consultas no histórico
        modelBuilder.Entity<UpdateHistoryEntity>()
            .HasIndex(u => u.UpdatedAt)
            .IsDescending();

        // Salvar o Enum Status como String no banco (ex: "Success" em vez de 0)
        modelBuilder.Entity<UpdateHistoryEntity>()
            .Property(u => u.Status)
            .HasConversion<string>();

        // Converter a List<string> (PackageIds) do Profile para uma coluna JSON
        modelBuilder.Entity<ProfileEntity>()
            .Property(p => p.PackageIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default), // Convert List<string> para JSON ao salvar
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );
    }

}
