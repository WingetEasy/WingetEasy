using System;
using System.Collections.Generic;
using System.Linq;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

/// <summary>
/// Interpreta os argumentos de linha de comando da aplicação.
/// </summary>
public sealed class CliArgumentsParser
{
    public bool TryParse(IEnumerable<string> args, out CliCommand command, out string? error)
    {
        var tokens = args.ToList();
        if (tokens.Count > 0 && !tokens[0].StartsWith("--", StringComparison.Ordinal))
        {
            tokens.RemoveAt(0);
        }

        command = CliCommand.None;
        error = null;

        bool hasCheck = false;
        bool hasUpdateAll = false;
        bool hasBackup = false;
        bool hasRestore = false;
        bool hasMinimized = false;
        string? backupPath = null;
        string? restorePath = null;

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            switch (token)
            {
                case "--dev":
                case "--elevated":
                case "--job":
                    if (token == "--job")
                    {
                        index++;
                    }
                    break;

                case "--check":
                    hasCheck = true;
                    break;

                case "--update-all":
                    hasUpdateAll = true;
                    break;

                case "--backup":
                    hasBackup = true;
                    if (index + 1 < tokens.Count && !tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        backupPath = tokens[index + 1];
                        index++;
                    }
                    break;

                case "--restore":
                    hasRestore = true;
                    if (index + 1 >= tokens.Count || tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "O argumento --restore exige um caminho de arquivo ou ID.";
                        return false;
                    }

                    restorePath = tokens[index + 1];
                    index++;
                    break;

                case "--minimized":
                    hasMinimized = true;
                    break;

                default:
                    error = $"Argumento inválido: {token}";
                    return false;
            }
        }

        var commandsCount = (hasCheck ? 1 : 0) + (hasUpdateAll ? 1 : 0) + (hasBackup ? 1 : 0) + (hasRestore ? 1 : 0);
        if (commandsCount > 1)
        {
            error = "Somente um comando headless pode ser executado por vez.";
            return false;
        }

        if (hasCheck)
        {
            command = new CliCommand(CliCommandType.Check);
            return true;
        }

        if (hasUpdateAll)
        {
            command = new CliCommand(CliCommandType.UpdateAll);
            return true;
        }

        if (hasBackup)
        {
            command = new CliCommand(CliCommandType.Backup, backupPath);
            return true;
        }

        if (hasRestore)
        {
            command = new CliCommand(CliCommandType.Restore, restorePath);
            return true;
        }

        if (hasMinimized)
        {
            command = new CliCommand(CliCommandType.Minimized);
            return true;
        }

        return true;
    }
}
