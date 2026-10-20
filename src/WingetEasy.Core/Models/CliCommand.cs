namespace WingetEasy.Core.Models;

public enum CliCommandType
{
    None,
    Check,
    UpdateAll,
    Backup,
    Restore,
    Minimized
}

public sealed record CliCommand(CliCommandType Type, string? Value = null)
{
    public static CliCommand None { get; } = new(CliCommandType.None);

    public bool IsHeadless => Type is CliCommandType.Check
        or CliCommandType.UpdateAll
        or CliCommandType.Backup
        or CliCommandType.Restore;
}
