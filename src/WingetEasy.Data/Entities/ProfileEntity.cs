
using System;
using System.Collections.Generic;

namespace WingetEasy.Data.Entities;

public class ProfileEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // convertido para Json no banco de dados
    public List<string> PackageIds { get; set; } = [];
}
