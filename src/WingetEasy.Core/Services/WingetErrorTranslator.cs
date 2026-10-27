using System;
using System.Collections.Generic;
using System.Linq;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Services;

/// <summary>
/// Traduz os códigos de saída (HRESULT) do WinGet e dos instaladores para mensagens claras em português,
/// indicando também a ação automática de recuperação que o WingetEasy deve tomar.
/// </summary>
public static class WingetErrorTranslator
{
    /// <summary>
    /// Traduz um código de saída do WinGet.
    /// </summary>
    /// <param name="exitCode">Código de saída do processo winget.exe.</param>
    /// <param name="output">Linhas relevantes do stdout/stderr (opcional), usadas como fallback de detecção.</param>
    /// <param name="isInstallVerb">True quando o comando foi 'winget install' (e não 'upgrade').</param>
    public static WingetErrorInfo Translate(int exitCode, IEnumerable<string>? output = null, bool isInstallVerb = false)
    {
        var info = TranslateCode(exitCode, isInstallVerb);

        // O WinGet às vezes devolve um código genérico, mas imprime o HRESULT real no stdout.
        if ((info.Kind is WingetErrorKind.Unknown or WingetErrorKind.InstallerFailed) && output is not null)
        {
            var refined = TranslateFromOutput(exitCode, output, isInstallVerb);
            if (refined is not null)
                return refined;
        }

        return info;
    }

    private static WingetErrorInfo TranslateCode(int exitCode, bool isInstallVerb)
    {
        switch (exitCode)
        {
            case WingetExitCodes.Success:
                return new(exitCode, WingetErrorKind.Success, "Atualizado com sucesso.", IsSuccess: true);

            // ── Sucesso com reinicialização pendente ──
            case WingetExitCodes.InstallRebootRequiredToFinish:
            case WingetExitCodes.InstallRebootInitiated:
            case WingetExitCodes.MsiRebootRequired:
            case WingetExitCodes.MsiRebootInitiated:
            case WingetExitCodes.HResultRebootRequired:
                return new(exitCode, WingetErrorKind.SuccessRebootRequired,
                    "Atualizado com sucesso! Reinicie o Windows para concluir.",
                    IsSuccess: true, RequiresReboot: true);

            // ── Já atualizado ──
            case WingetExitCodes.NoApplicableUpdates:
                return new(exitCode, WingetErrorKind.AlreadyUpToDate,
                    "O pacote já está na versão mais recente compatível com seu sistema.",
                    IsSuccess: true);

            case WingetExitCodes.PackageAlreadyInstalled:
            case WingetExitCodes.InstallAlreadyInstalled:
                return new(exitCode, WingetErrorKind.AlreadyUpToDate,
                    "O pacote já está instalado.",
                    IsSuccess: isInstallVerb || exitCode == WingetExitCodes.PackageAlreadyInstalled);

            // ── Aplicativo em execução ──
            case WingetExitCodes.InstallPackageInUse:
            case WingetExitCodes.InstallPackageInUseByApplication:
            case WingetExitCodes.InstallFileInUse:
                return new(exitCode, WingetErrorKind.AppRunning,
                    "O programa está aberto. Feche-o e tente novamente.");

            // ── Hash divergente → atualizar fontes e tentar de novo ──
            case WingetExitCodes.InstallerHashMismatch:
                return new(exitCode, WingetErrorKind.HashMismatch,
                    "O instalador baixado não corresponde ao esperado (hash divergente). O fabricante pode ter publicado uma nova versão; tente novamente mais tarde.",
                    ShouldRetryAfterSourceUpdate: true);

            // ── Requer administrador → lote elevado (UAC) ──
            case WingetExitCodes.Win32AccessDenied:
            case WingetExitCodes.HResultAccessDenied:
            case WingetExitCodes.Win32ElevationRequired:
            case WingetExitCodes.HResultElevationRequired:
                return new(exitCode, WingetErrorKind.RequiresElevation,
                    "Esta atualização requer permissões de administrador.",
                    RequiresElevation: true);

            case WingetExitCodes.InstallerProhibitsElevation:
                return new(exitCode, WingetErrorKind.NotSupported,
                    "Este instalador não pode ser executado como administrador. Abra o WingetEasy sem elevação e tente novamente.");

            // ── Instalador falhou (possivelmente não suporta modo silencioso) ──
            case WingetExitCodes.ShellExecInstallFailed:
            case WingetExitCodes.InstallInvalidParameter:
            case WingetExitCodes.InstallContactSupport:
                return new(exitCode, WingetErrorKind.InstallerFailed,
                    "O instalador falhou no modo silencioso. Tente a atualização interativa.",
                    CanRetryInteractive: true);

            case WingetExitCodes.InstallUpgradeNotSupported:
                return new(exitCode, WingetErrorKind.NotSupported,
                    "Este instalador não suporta atualizar a versão existente. Tente a atualização interativa ou reinstale o programa.",
                    CanRetryInteractive: true);

            // ── Demais erros conhecidos ──
            case WingetExitCodes.InstallRebootRequiredForInstall:
                return new(exitCode, WingetErrorKind.RebootRequiredBeforeInstall,
                    "Há uma reinicialização pendente no Windows. Reinicie o computador e tente novamente.");

            case WingetExitCodes.InstallInProgress:
                return new(exitCode, WingetErrorKind.InstallInProgress,
                    "Outra instalação está em andamento. Aguarde a conclusão e tente novamente.");

            case WingetExitCodes.InstallNoNetwork:
            case WingetExitCodes.DownloadFailed:
                return new(exitCode, WingetErrorKind.NoNetwork,
                    "Não foi possível baixar o instalador. Verifique sua conexão com a internet e tente novamente.");

            case WingetExitCodes.InstallDiskFull:
                return new(exitCode, WingetErrorKind.DiskFull,
                    "Espaço em disco insuficiente. Libere espaço e tente novamente.");

            case WingetExitCodes.InstallInsufficientMemory:
                return new(exitCode, WingetErrorKind.InsufficientMemory,
                    "Memória insuficiente para concluir a instalação. Feche outros programas e tente novamente.");

            case WingetExitCodes.NoApplicationsFound:
                return new(exitCode, WingetErrorKind.NotFound,
                    "O pacote não foi encontrado nas fontes do WinGet.");

            case WingetExitCodes.InstallBlockedByPolicy:
                return new(exitCode, WingetErrorKind.BlockedByPolicy,
                    "A instalação foi bloqueada por uma política do sistema ou da organização.");

            case WingetExitCodes.InstallSystemNotSupported:
                return new(exitCode, WingetErrorKind.NotSupported,
                    "Esta versão do pacote não é compatível com o seu sistema.");

            case WingetExitCodes.InstallDowngrade:
                return new(exitCode, WingetErrorKind.NotSupported,
                    "Uma versão mais recente já está instalada.");

            case WingetExitCodes.InstallMissingDependency:
            case WingetExitCodes.InstallDependencies:
                return new(exitCode, WingetErrorKind.InstallerFailed,
                    "Falha ao instalar as dependências do pacote.");

            case WingetExitCodes.InstallCancelledByUser:
                return new(exitCode, WingetErrorKind.Cancelled,
                    "A instalação foi cancelada.");
        }

        // Códigos fora da faixa do WinGet (0x8A15xxxx) normalmente são códigos crus do instalador.
        // Nesses casos, o modo silencioso pode não ser suportado: oferecemos a tentativa interativa.
        var isWingetRange = (unchecked((uint)exitCode) & 0xFFFF0000u) == 0x8A150000u;
        return new(exitCode, WingetErrorKind.Unknown,
            $"Falha na atualização (código 0x{unchecked((uint)exitCode):X8}).",
            CanRetryInteractive: !isWingetRange);
    }

    private static WingetErrorInfo? TranslateFromOutput(int exitCode, IEnumerable<string> output, bool isInstallVerb)
    {
        var lines = output.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0) return null;

        // 1. HRESULT impresso explicitamente no output (ex.: "0x8a150101 : ...")
        foreach (var line in lines)
        {
            var idx = line.IndexOf("0x8a15", StringComparison.OrdinalIgnoreCase);
            if (idx < 0 || idx + 10 > line.Length) continue;

            var hex = line.Substring(idx + 2, 8);
            if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
            {
                var code = unchecked((int)parsed);
                if (code != exitCode)
                {
                    var info = TranslateCode(code, isInstallVerb);
                    if (info.Kind is not WingetErrorKind.Unknown)
                        return info with { ExitCode = exitCode };
                }
            }
        }

        // 2. Frases conhecidas (EN / PT-BR)
        var combined = string.Join(" ", lines);
        if (combined.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
            (combined.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
             combined.Contains("does not match", StringComparison.OrdinalIgnoreCase) ||
             combined.Contains("não corresponde", StringComparison.OrdinalIgnoreCase)))
        {
            return TranslateCode(WingetExitCodes.InstallerHashMismatch, isInstallVerb) with { ExitCode = exitCode };
        }

        if (combined.Contains("restart", StringComparison.OrdinalIgnoreCase) &&
            combined.Contains("successfully installed", StringComparison.OrdinalIgnoreCase))
        {
            return TranslateCode(WingetExitCodes.InstallRebootRequiredToFinish, isInstallVerb) with { ExitCode = exitCode };
        }

        return null;
    }

    /// <summary>
    /// Monta a mensagem final exibida ao usuário, incluindo o código hexadecimal para diagnóstico em falhas.
    /// </summary>
    public static string FormatMessage(WingetErrorInfo info)
    {
        if (info.IsSuccess || info.Kind == WingetErrorKind.Unknown)
            return info.Message;

        return $"{info.Message} ({info.HexCode})";
    }
}
