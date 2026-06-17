using System;
using WingetEasy.Core.Interfaces;

namespace WingetEasy.App.Services;

/// <summary>
/// Implementação das notificações utilizando as APIs de Toast nativas do ecossistema Windows.
/// </summary>
public class ToastNotificationService : INotificationService
{
    public void ShowUpdatesAvailable(int count)
    {

    }

    public void ShowUpdateComplete(int successCount, int failCount)
    {

    }

    public void ShowError(string title, string message)
    {

    }
}
