namespace WingetEasy.Core.Models;

/// <summary>
/// Categoria semântica de um código de saída do WinGet, usada para decidir a ação automática.
/// </summary>
public enum WingetErrorKind
{
    Success,
    SuccessRebootRequired,
    AlreadyUpToDate,
    AppRunning,
    HashMismatch,
    RequiresElevation,
    InstallerFailed,
    RebootRequiredBeforeInstall,
    InstallInProgress,
    NoNetwork,
    DiskFull,
    InsufficientMemory,
    NotFound,
    BlockedByPolicy,
    NotSupported,
    Cancelled,
    Unknown
}

/// <summary>
/// Resultado da tradução de um código de saída do WinGet para uma mensagem amigável e uma ação.
/// </summary>
/// <param name="ExitCode">Código de saída original.</param>
/// <param name="Kind">Categoria semântica.</param>
/// <param name="Message">Mensagem clara em português para o usuário.</param>
/// <param name="IsSuccess">Se a operação deve ser considerada bem-sucedida.</param>
/// <param name="RequiresReboot">Se é necessário reiniciar o Windows para concluir.</param>
/// <param name="ShouldRetryAfterSourceUpdate">Se deve executar 'winget source update' e tentar novamente.</param>
/// <param name="RequiresElevation">Se deve ser reencaminhado ao lote elevado (UAC).</param>
/// <param name="CanRetryInteractive">Se faz sentido oferecer uma nova tentativa interativa (sem --silent).</param>
public sealed record WingetErrorInfo(
    int ExitCode,
    WingetErrorKind Kind,
    string Message,
    bool IsSuccess = false,
    bool RequiresReboot = false,
    bool ShouldRetryAfterSourceUpdate = false,
    bool RequiresElevation = false,
    bool CanRetryInteractive = false)
{
    /// <summary>Código formatado em hexadecimal (ex.: 0x8A150101).</summary>
    public string HexCode => $"0x{unchecked((uint)ExitCode):X8}";

    /// <summary>Indica se o pacote está aberto e precisa ser fechado.</summary>
    public bool IsAppRunning => Kind == WingetErrorKind.AppRunning;
}

