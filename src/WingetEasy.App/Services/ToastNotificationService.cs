using System;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Serilog;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Services;

namespace WingetEasy.App.Services;

/// <summary>
/// Implementação das notificações utilizando as APIs de Toast nativas do ecossistema Windows.
/// Quando o app roda elevado (administrador), o AppNotification não é suportado; nesse caso
/// usa um balão pela bandeja do sistema como alternativa.
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    private static readonly bool IsElevated = SafeIsElevated();

    public void Register()
    {
        if (IsElevated)
        {
            Log.Information("Aplicativo elevado: notificações nativas indisponíveis; usando balões da bandeja.");
            return;
        }

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
        Show(
            "Atualizações disponíveis",
            $"{count} pacote(s) podem ser atualizados.",
            builder => builder
                .AddButton(new AppNotificationButton("Ver agora").AddArgument("action", "viewUpdates"))
                .AddButton(new AppNotificationButton("Atualizar tudo").AddArgument("action", "updateAll")),
            "Não foi possível exibir a notificação de atualizações.");
    }

    public void ShowUpdateComplete(int successCount, int failCount)
    {
        Show(
            "Atualização concluída",
            $"{successCount} pacote(s) atualizado(s); {failCount} falha(s).",
            null,
            "Não foi possível exibir a notificação de conclusão.");
    }

    public void ShowError(string title, string message)
    {
        Show(title, message, null, "Não foi possível exibir a notificação de erro.");
    }

    private static void Show(string title, string message, Func<AppNotificationBuilder, AppNotificationBuilder>? customize, string errorMessage)
    {
        try
        {
            if (IsElevated)
            {
                (Application.Current as App)?.ShowTrayNotification(title, message);
                return;
            }

            var builder = new AppNotificationBuilder().AddText(title).AddText(message);
            if (customize is not null)
            {
                builder = customize(builder);
            }

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            Log.Error(ex, errorMessage);
        }
    }

    private static bool SafeIsElevated()
    {
        try
        {
            return new AdminElevationHelper().IsRunningAsAdmin();
        }
        catch
        {
            return false;
        }
    }
}