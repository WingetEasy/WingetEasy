
using System;

namespace WingetEasy.Data.Entities;

public class UpdateHistoryEntity
{
    public int Id { get; set; }
    public required string PackageId { get; set; }
    public required string PackageName { get; set; }
    public required string FromVersion { get; set; }
    public required string ToVersion { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public UpdateStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
}


