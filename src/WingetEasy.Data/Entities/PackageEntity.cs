using System;

namespace WingetEasy.Data.Entities;

public class PackageEntity
{
    public int Id { get; set; }
    public required string WingetId { get; set; }
    public required string Name { get; set; }
    public required string Source { get; set; }
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
    public DateTime? SkippedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
