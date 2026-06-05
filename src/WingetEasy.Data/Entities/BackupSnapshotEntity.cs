
using System;

namespace WingetEasy.Data.Entities;

public class BackupSnapshotEntity
{
    public Guid Id { get; set; }
    public required string Label { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int PackageCount { get; set; }
    public required string WingetVersion { get; set; }
    public required string Data { get; set; } // string json
    
}
