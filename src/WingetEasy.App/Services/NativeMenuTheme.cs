using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace WingetEasy.App.Services;

/// <summary>
/// Aplica o tema Claro/Escuro/Sistema aos menus nativos (Win32) do processo, como o menu da bandeja
/// emulado pelo H.NotifyIcon no modo PopupMenu. Usa as funções de tema de menu do uxtheme.dll
/// (ordinais 135 e 136), a mesma abordagem usada por apps como o Windows Terminal e o Explorer.
/// Se o Windows não expuser essas funções, o menu simplesmente segue o tema padrão do sistema.
/// </summary>
internal static class NativeMenuTheme
{
    private enum PreferredAppMode
    {
        Default = 0,
        AllowDark = 1,
        ForceDark = 2,
        ForceLight = 3
    }

    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(PreferredAppMode mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    public static void Apply(ElementTheme theme)
    {
        try
        {
            var mode = theme switch
            {
                ElementTheme.Dark => PreferredAppMode.ForceDark,
                ElementTheme.Light => PreferredAppMode.ForceLight,
                _ => PreferredAppMode.AllowDark // Sistema: segue o tema do Windows
            };

            SetPreferredAppMode(mode);
            FlushMenuThemes();
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            // Versão do Windows sem suporte: mantém o visual padrão.
        }
    }
}
