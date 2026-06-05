
using System;

namespace WingetEasy.Data.Entities;

public class CheckLogEntity
{
    public int Id { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public int FoundCount { get; set; }
    public bool DurationMs { get; set; }
}
