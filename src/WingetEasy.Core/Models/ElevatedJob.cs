
using System.Collections.Generic;

namespace WingetEasy.Core.Models;

public class ElevatedJob
{
    public string JobId { get; set; } = string.Empty;
    public List<string> PackageIds { get; set; } = [];
    
}
