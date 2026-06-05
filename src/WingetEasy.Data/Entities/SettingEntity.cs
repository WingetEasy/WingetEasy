
using System;

namespace WingetEasy.Data.Entities;

public class SettingEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
