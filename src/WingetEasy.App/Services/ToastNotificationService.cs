using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Serilog;
using WingetEasy.Core.Interfaces;

namespace WingetEasy.App.Services;

/// <summary>
/// Implementação das notificações utilizando as APIs de Toast nativas do ecossistema Windows.
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    public void Register()
    {
        try
        {
            AppNotificationManager.Default.Register();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível registrar notificações nativas. O aplicativo pode estar em execução sem identidade.");
        }
    }

    public void ShowUpdatesAvailable(int count)
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText("Atualizações disponíveis")
                .AddText($"{count} pacote(s) podem ser atualizados.")
                .AddButton(new AppNotificationButton("Ver agora").AddArgument("action", "viewUpdates"))
                .AddButton(new AppNotificationButton("Atualizar tudo").AddArgument("action", "updateAll"))
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Não foi possível exibir a notificação de atualizações.");
        }
    }

    public void ShowUpdateComplete(int successCount, int failCount)
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText("Atualização concluída")
                .AddText($"{successCount} pacote(s) atualizado(s); {failCount} falha(s).")
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Não foi possível exibir a notificação de conclusão.");
        }
    }

    public void ShowError(string title, string message)
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Não foi possível exibir a notificação de erro.");
        }
    }
}
