using System;
using Microsoft.Win32;

namespace WingetEasy.App.Services;

/// <summary>
/// Única fonte de verdade para "Iniciar com o Windows" (HKCU\...\Run), usada pelo Onboarding e pelas Configurações.
/// </summary>
internal static class StartupRegistration
{
    public const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "WingetEasy";
    public const string MinimizedArgument = "--minimized";

    // Nome usado por versões anteriores das Configurações; removido para evitar entradas duplicadas.
    private const string LegacyValueName = "WingetEasyApp";

    /// <summary>
    /// Indica se a inicialização com o Windows está ativa (considera também a chave legada).
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) != null || key?.GetValue(LegacyValueName) != null;
    }

    /// <summary>
    /// Ativa (sempre com --minimized) ou desativa a inicialização com o Windows, removendo entradas legadas.
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException("Não foi possível acessar as configurações de inicialização do Windows.");

        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(executablePath))
        {
            throw new InvalidOperationException("Não foi possível determinar o executável do aplicativo.");
        }

        key.SetValue(ValueName, $"\"{executablePath}\" {MinimizedArgument}");
    }
}