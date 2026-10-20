using System;
using System.Collections.Generic;

namespace WingetEasy.Core.Models;

public record Profile(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    IReadOnlyList<ProfilePackage> Packages
);

public record ProfilePackage(string Id, string Name);
