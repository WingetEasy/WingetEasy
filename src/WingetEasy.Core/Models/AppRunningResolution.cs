namespace WingetEasy.Core.Models;

/// <summary>
/// Ação escolhida para um aplicativo que está atualmente em execução durante uma atualização.
/// </summary>
public enum AppRunningResolution
{
    /// <summary>
    /// Pula a atualização por enquanto, adiando-a para um momento posterior.
    /// </summary>
    Skip,

    /// <summary>
    /// Fecha o aplicativo em execução e prossegue com a atualização.
    /// </summary>
    CloseAndUpdate
}

