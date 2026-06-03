namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Abstrai a apresentação de alertas visuais ao utilizador, desacoplando a lógica de negócio do
/// sistema de notificações nativo do Windows (Toast Notifications / Action Center).
/// </summary>

public interface INotificationService
{
    void ShowUpdatesAvailable(int count);
    void ShowUpdateComplete(int successCount, int failCount);
    void ShowError(string title, string message);
}
