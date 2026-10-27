using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Serilog;
using WinRT;

namespace WingetEasy.App.Services;

/// <summary>
/// Configura o DesktopAcrylicController (mesmo efeito translúcido da Barra de Tarefas do Windows 11)
/// mantendo as cores exatas do WingetEasy (#2B2B31 no escuro e #F8FAFC no claro) sem ficar preto
/// e sem perder a transparência quando a janela está fora de foco.
/// </summary>
public sealed class AcrylicBackdropHelper : IDisposable
{
    private readonly Window _window;
    private DesktopAcrylicController? _acrylicController;
    private MicaController? _micaController;
    private SystemBackdropConfiguration? _configurationSource;
    [ThreadStatic]
    private static IntPtr _dispatcherQueueController;
    private bool _disposed;

    private static readonly Windows.UI.Color DarkTintColor = Windows.UI.Color.FromArgb(255, 0x2B, 0x2B, 0x31);
    private static readonly Windows.UI.Color LightTintColor = Windows.UI.Color.FromArgb(255, 0xF8, 0xFA, 0xFC);

    public AcrylicBackdropHelper(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public void TryActivate()
    {
        if (_disposed) return;

        try
        {
            EnsureWindowsSystemDispatcherQueueController();
            _window.SystemBackdrop = null;

            bool isLight = ResolveIsLightTheme();
            _configurationSource = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = isLight ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark
            };

            if (DesktopAcrylicController.IsSupported())
            {
                _acrylicController = new DesktopAcrylicController();
                ApplyAcrylicProperties(isLight);

                var target = _window.As<ICompositionSupportsSystemBackdrop>();
                _acrylicController.AddSystemBackdropTarget(target);
                _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
            }
            else if (MicaController.IsSupported())
            {
                _micaController = new MicaController { Kind = MicaKind.Base };
                ApplyMicaProperties(isLight);

                var target = _window.As<ICompositionSupportsSystemBackdrop>();
                _micaController.AddSystemBackdropTarget(target);
                _micaController.SetSystemBackdropConfiguration(_configurationSource);
            }

            if (_window.Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged += OnActualThemeChanged;
            }

            _window.Closed += OnWindowClosed;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível ativar AcrylicBackdropHelper customizado; usando fallback.");
            try
            {
                if (DesktopAcrylicController.IsSupported())
                {
                    _window.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                }
            }
            catch
            {
            }
        }
    }

    public void UpdateTheme(ElementTheme theme)
    {
        if (_disposed || _configurationSource == null) return;

        bool isLight = theme switch
        {
            ElementTheme.Light => true,
            ElementTheme.Dark => false,
            _ => ResolveIsLightTheme()
        };

        _configurationSource.IsInputActive = true;
        _configurationSource.Theme = isLight ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;

        if (_acrylicController != null)
        {
            ApplyAcrylicProperties(isLight);
        }
        else if (_micaController != null)
        {
            ApplyMicaProperties(isLight);
        }
    }

    private void ApplyAcrylicProperties(bool isLight)
    {
        if (_acrylicController == null) return;

        if (isLight)
        {
            _acrylicController.TintColor = LightTintColor;
            _acrylicController.FallbackColor = LightTintColor;
            _acrylicController.TintOpacity = 0.40f;
            _acrylicController.LuminosityOpacity = 0.55f;
        }
        else
        {
            _acrylicController.TintColor = DarkTintColor;
            _acrylicController.FallbackColor = DarkTintColor;
            _acrylicController.TintOpacity = 1.0f;
            _acrylicController.LuminosityOpacity = 0.80f;
        }
    }

    private void ApplyMicaProperties(bool isLight)
    {
        if (_micaController == null) return;

        if (isLight)
        {
            _micaController.TintColor = LightTintColor;
            _micaController.FallbackColor = LightTintColor;
            _micaController.TintOpacity = 0.50f;
            _micaController.LuminosityOpacity = 0.75f;
        }
        else
        {
            _micaController.TintColor = DarkTintColor;
            _micaController.FallbackColor = DarkTintColor;
            _micaController.TintOpacity = 0.45f;
            _micaController.LuminosityOpacity = 0.65f;
        }
    }

    private bool ResolveIsLightTheme()
    {
        if (_window.Content is FrameworkElement fe)
        {
            if (fe.ActualTheme == ElementTheme.Light) return true;
            if (fe.ActualTheme == ElementTheme.Dark) return false;
        }

        try
        {
            return Application.Current.RequestedTheme == ApplicationTheme.Light;
        }
        catch
        {
            return false;
        }
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateTheme(sender.ActualTheme);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_window.Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= OnActualThemeChanged;
            }
        }
        catch
        {
        }

        _acrylicController?.Dispose();
        _acrylicController = null;

        _micaController?.Dispose();
        _micaController = null;

        _configurationSource = null;
    }

    private static void EnsureWindowsSystemDispatcherQueueController()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
        {
            return;
        }

        if (_dispatcherQueueController == IntPtr.Zero)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf<DispatcherQueueOptions>();
            options.threadType = 2;    // DQTYPE_THREAD_CURRENT
            options.apartmentType = 2; // DQTAT_COM_STA

            _ = CreateDispatcherQueueController(options, out _dispatcherQueueController);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        out IntPtr dispatcherQueueController);
}

