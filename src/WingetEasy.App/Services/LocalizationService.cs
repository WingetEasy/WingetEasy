using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WingetEasy.Core.Interfaces;

namespace WingetEasy.App.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    bool IsPortuguese { get; }
    void SetLanguage(string lang, bool persist = true);
    string Get(string textOrKey);
    void LocalizeElement(DependencyObject? root);
    void LocalizeFlyout(FlyoutBase? flyoutBase);
    event Action? LanguageChanged;
}

public sealed class LocalizationService : ILocalizationService
{
    private static readonly Lazy<LocalizationService> _lazyInstance = new(() => new LocalizationService());
    public static LocalizationService Instance => _lazyInstance.Value;

    public string CurrentLanguage { get; private set; } = "pt";
    public bool IsPortuguese => CurrentLanguage == "pt";

    public event Action? LanguageChanged;

    private readonly Dictionary<string, string> _ptToEn = new(StringComparer.OrdinalIgnoreCase)
    {
        // Sidebar & Header
        ["BIBLIOTECA"] = "LIBRARY",
        ["LIBRARY"] = "LIBRARY",
        ["Atualizações"] = "Updates",
        ["Histórico"] = "History",
        ["Backup"] = "Backup",
        ["Perfis"] = "Profiles",
        ["Configurações"] = "Settings",
        ["Alternar tema Claro / Escuro"] = "Toggle Light / Dark theme",
        ["Alternar tema Claro ou Escuro"] = "Toggle Light / Dark theme",
        ["Reiniciar agora"] = "Restart now",

        // UpdatesPage - Hero
        ["Atualizar tudo"] = "Update all",
        ["Verificar agora"] = "Check now",
        ["Verificando atualizações…"] = "Checking for updates…",
        ["Verificando atualizações..."] = "Checking for updates...",
        ["Atualizando pacotes…"] = "Updating packages…",
        ["Atualizando pacotes..."] = "Updating packages...",
        ["Sistema atualizado"] = "System up to date",
        ["Tudo atualizado"] = "All up to date",
        ["Consultando as fontes do Winget."] = "Querying Winget sources.",
        ["Recomendamos atualizar para manter tudo seguro e rápido."] = "We recommend updating to keep everything secure and fast.",
        ["Seu sistema está em dia. Última verificação agora há pouco."] = "Your system is up to date. Last check just now.",
        ["Resumo da atualização"] = "Update summary",

        // UpdatesPage - Sections
        ["Adiados"] = "Deferred",
        ["Você optou por não atualizar da última vez"] = "You chose not to update last time",
        ["Disponíveis"] = "Available",
        ["Buscar por nome ou ID..."] = "Search by name or ID...",
        ["Buscar por nome ou ID"] = "Search by name or ID",
        ["Todos"] = "All",
        ["App"] = "App",
        ["Runtime"] = "Runtime",
        ["Segurança"] = "Security",
        ["Todos os pacotes estão atualizados."] = "All packages are up to date.",
        ["Seu sistema está em dia."] = "Your system is up to date.",

        // UpdatesPage - Background Card
        ["Rodando em segundo plano"] = "Running in background",
        ["Notificamos você quando houver novidades. Sem terminal."] = "We notify you when updates arrive. No terminal.",
        ["Atualização automática"] = "Automatic update",
        ["Próxima verificação:"] = "Next check:",

        // UpdatesPage - Item Control & Action
        ["Atualizar"] = "Update",
        ["Atualizado"] = "Updated",
        ["Pendente"] = "Pending",
        ["Atualizando..."] = "Updating...",
        ["Atualizando…"] = "Updating…",
        ["Concluído"] = "Completed",
        ["Falha"] = "Failed",
        ["Adiar"] = "Defer",
        ["Ignorar"] = "Ignore",
        ["Adiar (Ignorar esta vez)"] = "Defer (Skip this time)",
        ["Ignorar sempre"] = "Always ignore",
        ["Copiar ID"] = "Copy ID",
        ["Ver no Winget"] = "View on Winget",
        ["Mais opções"] = "More options",

        // UpdateFlyout (Quick Panel)
        ["Nenhuma atualização disponível no momento."] = "No updates available at the moment.",
        ["Abrir"] = "Open",
        ["Fechar"] = "Close",
        ["Atualizando:"] = "Updating:",
        ["Nenhum selecionado"] = "None selected",

        // HistoryPage
        ["Hoje"] = "Today",
        ["Ontem"] = "Yesterday",
        ["7 dias"] = "7 days",
        ["Mês"] = "Month",
        ["Tudo"] = "All",
        ["Buscar por nome ou ID do pacote"] = "Search by package name or ID",
        ["Exportar CSV"] = "Export CSV",
        ["Sucesso"] = "Success",
        ["Nenhum histórico encontrado."] = "No update history found.",
        ["Nenhuma atualização encontrada para o período selecionado."] = "No updates found for the selected period.",
        ["Nenhuma atualização corresponde à busca."] = "No updates match your search.",
        ["Histórico exportado com sucesso."] = "History exported successfully.",
        ["Não foi possível exportar o histórico."] = "Unable to export history.",

        // BackupPage
        ["Backup e restauração"] = "Backup & restore",
        ["Salve os pacotes instalados e restaure-os quando precisar."] = "Save installed packages and restore them whenever needed.",
        ["Rótulo opcional do snapshot"] = "Optional snapshot label",
        ["Criar Snapshot Agora"] = "Create Snapshot Now",
        ["Restaurar"] = "Restore",
        ["Exportar .ps1"] = "Export .ps1",
        ["Exportar .json"] = "Export .json",
        ["Excluir"] = "Delete",
        ["Nenhum snapshot salvo ainda."] = "No snapshots saved yet.",
        ["Crie um snapshot para preservar os pacotes instalados."] = "Create a snapshot to preserve installed packages.",
        ["Versão desconhecida"] = "Unknown version",
        ["Criando snapshot..."] = "Creating snapshot...",
        ["Não foi possível criar o snapshot."] = "Unable to create snapshot.",
        ["Não foi possível restaurar o snapshot."] = "Unable to restore snapshot.",
        ["Não foi possível excluir o snapshot."] = "Unable to delete snapshot.",
        ["Não foi possível carregar os snapshots salvos."] = "Unable to load saved snapshots.",

        // ProfilesPage
        ["Perfis de instalação"] = "Installation profiles",
        ["Nome"] = "Name",
        ["Descrição"] = "Description",
        ["ID|Nome (um por linha)"] = "ID|Name (one per line)",
        ["Salvar perfil"] = "Save profile",
        ["Cancelar"] = "Cancel",
        ["Carregar pacotes instalados"] = "Load installed packages",
        ["Instalar"] = "Install",
        ["Editar"] = "Edit",
        ["Nenhum perfil disponível."] = "No profiles available.",
        ["Crie um perfil para instalar seus pacotes favoritos."] = "Create a profile to install your favorite packages.",
        ["Perfil salvo com sucesso."] = "Profile saved successfully.",
        ["Perfil excluído."] = "Profile deleted.",
        ["Instalação do perfil concluída."] = "Profile installation completed.",

        // SettingsPage - General
        ["Geral"] = "General",
        ["Iniciar com o Windows"] = "Start with Windows",
        ["Abre na bandeja quando você liga o PC"] = "Opens in tray when PC starts up",
        ["Manter na bandeja do sistema"] = "Keep in system tray",
        ["Continua verificando em segundo plano ao fechar"] = "Keeps checking in background on close",

        // SettingsPage - Updates
        ["Atualizar automaticamente"] = "Update automatically",
        ["Aplica atualizações sem precisar confirmar"] = "Applies updates without prompting",
        ["Notificar antes de atualizar"] = "Notify before updating",
        ["Você decide o que entra em cada rodada"] = "You choose what to update each round",
        ["Frequência de verificação"] = "Check frequency",
        ["Com que frequência procurar atualizações"] = "How often to check for updates",
        ["A cada hora"] = "Every hour",
        ["A cada 6 horas"] = "Every 6 hours",
        ["Diariamente"] = "Daily",
        ["Semanalmente"] = "Weekly",
        ["Incluir versões beta"] = "Include beta versions",
        ["Considera pacotes de pré-lançamento"] = "Includes pre-release packages",

        // SettingsPage - Notifications
        ["Notificações"] = "Notifications",
        ["Avisar sobre segurança"] = "Security alerts",
        ["Destacar correções críticas como importantes"] = "Highlight critical fixes as important",
        ["Horário silencioso"] = "Quiet hours",
        ["Não notificar entre 22h e 8h"] = "Do not notify between 10 PM and 8 AM",
        ["Som de notificação"] = "Notification sound",
        ["Tocar um som ao notificar"] = "Play sound on notification",

        // SettingsPage - Appearance
        ["Aparência"] = "Appearance",
        ["Tema do aplicativo"] = "App theme",
        ["Claro, escuro ou seguir o Windows"] = "Light, dark, or follow Windows",
        ["Claro"] = "Light",
        ["Escuro"] = "Dark",
        ["Sistema"] = "System",

        // SettingsPage - Maintenance
        ["Manutenção"] = "Maintenance",
        ["Catálogo de fontes do Winget"] = "Winget source catalog",
        ["Redefine e atualiza as fontes locais caso a busca de pacotes apresente falhas"] = "Resets and updates local sources if package search fails",
        ["Reparar fontes do Winget"] = "Repair Winget sources",
        ["Manutenção do Winget"] = "Winget maintenance",
        ["Windows Package Manager"] = "Windows Package Manager",
        ["Pacotes ignorados"] = "Ignored packages",

        // SettingsPage - About
        ["Sobre"] = "About",
        ["Procurar atualizações do app"] = "Check for app updates",
        ["Ver no GitHub"] = "View on GitHub",
        ["Distribuído sob licença MIT · Não afiliado à Microsoft"] = "Distributed under MIT license · Not affiliated with Microsoft",

        // Dialogs & Elevation
        ["Permissão de administrador necessária"] = "Administrator permission required",
        ["Alguns pacotes exigem privilégios de Administrador para serem atualizados."] = "Some packages require administrator privileges to update.",
        ["Continuar com Administrador"] = "Continue with Administrator",
        ["Aplicativo em execução"] = "Application running",
        ["Fechar e atualizar"] = "Close and update",
        ["Pular por enquanto"] = "Skip for now",

        // Tray Icon Menu
        ["Painel rápido"] = "Quick panel",
        ["Sair"] = "Exit"
    };

    private readonly Dictionary<string, string> _enToPt = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService()
    {
        foreach (var pair in _ptToEn)
        {
            if (!_enToPt.ContainsKey(pair.Value))
            {
                _enToPt[pair.Value] = pair.Key;
            }
        }
    }

    public void SetLanguage(string lang, bool persist = true)
    {
        if (lang != "pt" && lang != "en") lang = "pt";
        CurrentLanguage = lang;

        var cultureName = lang == "pt" ? "pt-BR" : "en-US";
        var culture = new CultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        try
        {
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = cultureName;
        }
        catch
        {
            // Fallback seguro se não suportado no ambiente unpackaged
        }

        if (persist)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var settingsRepo = App.Services.GetService<ISettingsRepository>();
                    if (settingsRepo != null)
                    {
                        await settingsRepo.SetAsync(App.AppLanguageSettingsKey, lang).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Falha ao persistir idioma: {ex}");
                }
            });
        }

        LanguageChanged?.Invoke();
    }

    public string Get(string textOrKey)
    {
        if (string.IsNullOrWhiteSpace(textOrKey)) return textOrKey;

        if (IsPortuguese)
        {
            if (_enToPt.TryGetValue(textOrKey, out var pt))
            {
                return pt;
            }
            return textOrKey;
        }
        else
        {
            if (_ptToEn.TryGetValue(textOrKey, out var en))
            {
                return en;
            }
            return textOrKey;
        }
    }

    public void LocalizeElement(DependencyObject? root)
    {
        if (root == null) return;

        if (root is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
        {
            var translated = Get(tb.Text);
            if (!string.Equals(translated, tb.Text, StringComparison.Ordinal))
            {
                tb.Text = translated;
            }
        }
        else if (root is TextBox txt && !string.IsNullOrEmpty(txt.PlaceholderText))
        {
            var translated = Get(txt.PlaceholderText);
            if (!string.Equals(translated, txt.PlaceholderText, StringComparison.Ordinal))
            {
                txt.PlaceholderText = translated;
            }
        }
        else if (root is ComboBox cb)
        {
            LocalizeComboBox(cb);
        }
        else if (root is ComboBoxItem cbi && cbi.Content is string cbiStr && !string.IsNullOrEmpty(cbiStr))
        {
            var translated = Get(cbiStr);
            if (!string.Equals(translated, cbiStr, StringComparison.Ordinal))
            {
                cbi.Content = translated;
            }
        }
        else if (root is ContentControl cc && cc.Content is string s && !string.IsNullOrEmpty(s))
        {
            var translated = Get(s);
            if (!string.Equals(translated, s, StringComparison.Ordinal))
            {
                cc.Content = translated;
            }
        }

        if (root is Button btn && btn.Flyout != null)
        {
            LocalizeFlyout(btn.Flyout);
        }

        if (root is FrameworkElement fe)
        {
            if (fe.ContextFlyout != null)
            {
                LocalizeFlyout(fe.ContextFlyout);
            }

            var toolTip = ToolTipService.GetToolTip(fe);
            if (toolTip is string toolTipStr && !string.IsNullOrEmpty(toolTipStr))
            {
                var translated = Get(toolTipStr);
                if (!string.Equals(translated, toolTipStr, StringComparison.Ordinal))
                {
                    ToolTipService.SetToolTip(fe, translated);
                }
            }
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            LocalizeElement(child);
        }
    }

    public void LocalizeComboBox(ComboBox cb)
    {
        if (cb == null) return;

        foreach (var item in cb.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Content is string cbiStr && !string.IsNullOrEmpty(cbiStr))
            {
                var translated = Get(cbiStr);
                if (!string.Equals(translated, cbiStr, StringComparison.Ordinal))
                {
                    cbi.Content = translated;
                }
            }
        }
    }

    public void LocalizeFlyout(FlyoutBase? flyoutBase)
    {
        if (flyoutBase is MenuFlyout menuFlyout)
        {
            LocalizeMenuFlyoutItems(menuFlyout.Items);
        }
    }

    public void LocalizeMenuFlyoutItems(IList<MenuFlyoutItemBase> items)
    {
        foreach (var item in items)
        {
            if (item is MenuFlyoutItem mfi && !string.IsNullOrEmpty(mfi.Text))
            {
                var translated = Get(mfi.Text);
                if (!string.Equals(translated, mfi.Text, StringComparison.Ordinal))
                {
                    mfi.Text = translated;
                }
            }
            else if (item is MenuFlyoutSubItem sub)
            {
                if (!string.IsNullOrEmpty(sub.Text))
                {
                    var translated = Get(sub.Text);
                    if (!string.Equals(translated, sub.Text, StringComparison.Ordinal))
                    {
                        sub.Text = translated;
                    }
                }
                LocalizeMenuFlyoutItems(sub.Items);
            }
        }
    }
}
