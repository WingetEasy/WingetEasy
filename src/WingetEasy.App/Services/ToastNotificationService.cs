using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Serilog;
using WingetEasy.Core.Interfaces;
using WingetEasy.Core.Services;

namespace WingetEasy.App.Services;

/// <summary>
/// Implementação das notificações utilizando as APIs de Toast nativas do ecossistema Windows.
/// Respeita as preferências do usuário: Horário Silencioso (QuietHours), Som de Notificação
/// (NotificationSound) e Alertas de Segurança (SecurityAlerts).
/// Quando o app roda elevado (administrador), o AppNotification não é suportado; nesse caso
/// usa um balão pela bandeja do sistema como alternativa.
/// </summary>
public class ToastNotificationService : IToastNotificationService
{
    private static readonly bool IsElevated = SafeIsElevated();
    private readonly ISettingsRepository? _settingsRepository;

    private bool _quietHours = true;
    private bool _notificationSound = false;
    private bool _securityAlerts = true;

    // Provedor de horário atual para facilitar testes unitários de Horário Silencioso
    internal Func<DateTime> CurrentTimeProvider { get; set; } = () => DateTime.Now;

    public bool QuietHoursEnabled => _quietHours;
    public bool NotificationSoundEnabled => _notificationSound;
    public bool SecurityAlertsEnabled => _securityAlerts;

    public ToastNotificationService(ISettingsRepository? settingsRepository = null)
    {
        _settingsRepository = settingsRepository;
        if (_settingsRepository != null)
        {
            _settingsRepository.SettingChanged += OnSettingChanged;
            _ = InitializeSettingsAsync();
        }
    }

    public async Task InitializeSettingsAsync()
    {
        if (_settingsRepository == null) return;

        try
        {
            var q = await _settingsRepository.GetAsync("QuietHours").ConfigureAwait(false);
            _quietHours = !bool.TryParse(q, out var qVal) || qVal; // default true

            var s = await _settingsRepository.GetAsync("NotificationSound").ConfigureAwait(false);
            _notificationSound = bool.TryParse(s, out var sVal) && sVal; // default false

            var sec = await _settingsRepository.GetAsync("SecurityAlerts").ConfigureAwait(false);
            _securityAlerts = !bool.TryParse(sec, out var secVal) || secVal; // default true
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Erro ao carregar configurações no ToastNotificationService.");
        }
    }

    private void OnSettingChanged(object? sender, string key)
    {
        if (key is "QuietHours" or "NotificationSound" or "SecurityAlerts")
        {
            _ = InitializeSettingsAsync();
        }
    }

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

    public bool IsQuietHoursActive()
    {
        if (!_quietHours) return false;
        var hour = CurrentTimeProvider().Hour;
        return hour >= 22 || hour < 8;
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

    public void ShowSecurityUpdatesAvailable(int count)
    {
        if (!_securityAlerts)
        {
            ShowUpdatesAvailable(count);
            return;
        }

        Show(
            "Alerta de Segurança",
            $"{count} atualização(ões) de segurança crítica(s) disponível(is). Recomendamos atualizar agora.",
            builder => builder
                .AddButton(new AppNotificationButton("Ver agora").AddArgument("action", "viewUpdates"))
                .AddButton(new AppNotificationButton("Atualizar tudo").AddArgument("action", "updateAll")),
            "Não foi possível exibir o alerta de segurança.");
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

    private void Show(string title, string message, Func<AppNotificationBuilder, AppNotificationBuilder>? customize, string errorMessage)
    {
        try
        {
            if (IsQuietHoursActive())
            {
                Log.Information("Notificação suprimida pelo Horário Silencioso (22h–8h): {Title}", title);
                return;
            }

            if (IsElevated)
            {
                (Application.Current as App)?.ShowTrayNotification(title, message);
                return;
            }

            var builder = new AppNotificationBuilder().AddText(title).AddText(message);
            if (!_notificationSound)
            {
                builder.MuteAudio();
            }

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
