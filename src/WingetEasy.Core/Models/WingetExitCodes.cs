namespace WingetEasy.Core.Models;

/// <summary>
/// Códigos de saída (Exit Codes / HRESULT) oficiais do WinGet CLI.
/// </summary>
public static class WingetExitCodes
{
    public const int Success = 0;

    /// <summary>
    /// Código 0x8A150056 (-1978335146): O aplicativo está atualmente em execução.
    /// </summary>
    public const int AppRunning = unchecked((int)0x8A150056);

    /// <summary>
    /// Código 0x8A15002B (-1978335189): Nenhuma atualização aplicável ou pacote já atualizado.
    /// </summary>
    public const int NoApplicableUpdates = -1978335189;

    /// <summary>
    /// Código 0x8A150014 (-1978335212): A instalação requer elevação de Administrador.
    /// </summary>
    public const int ElevationRequired = unchecked((int)0x8A150014);

    /// <summary>
    /// Código 0x8A15004E (-1978335154): Reinicialização pendente para concluir a instalação.
    /// </summary>
    public const int RebootPending = unchecked((int)0x8A15004E);
}

