namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Define o contrato para notificações interativas nativas do Windows.
/// </summary>
public interface IToastNotificationService : INotificationService
{
    /// <summary>
    /// Registra o aplicativo para receber ativações de notificações.
    /// </summary>
    void Register();
}
