using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace WingetEasy.App.Views;

public sealed partial class UpdateFlyout : Window
{
    private AppWindow _appWindow;
    private IntPtr _hwnd;
    private bool _isClosing = false;

    public UpdateFlyout()
    {
        this.InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // 1. CONFIGURAÇÃO DO PRESENTER (Remove a barra nativa e trava o redimensionamento)
        var presenter = _appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true; // Mantém a notificação sempre visível

            // A MÁGICA: Mantém a borda da janela, mas extermina a barra de título nativa!
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        // 2. Aplica o Fundo Translúcido (Acrylic)
        if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
        }

        HideFromTaskbarAndAltTab();
        PositionWindow();
    }

    private void HideFromTaskbarAndAltTab()
    {
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
    }

    private void PositionWindow()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        uint dpi = GetDpiForWindow(_hwnd);
        double scale = dpi / 96.0;

        int width = (int)(380 * scale);
        int height = (int)(560 * scale);

        int padding = (int)(12 * scale);
        int x = workArea.X + workArea.Width - width - padding;
        int y = workArea.Y + workArea.Height - height - padding;

        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(RootGrid, true);

        var visual = ElementCompositionPreview.GetElementVisual(RootGrid);
        var compositor = visual.Compositor;

        // Animação de Deslize (Slide Up)
        var slideAnim = compositor.CreateVector3KeyFrameAnimation();
        slideAnim.InsertKeyFrame(0f, new Vector3(0, 80, 0));
        slideAnim.InsertKeyFrame(1f, new Vector3(0, 0, 0));
        slideAnim.Duration = TimeSpan.FromMilliseconds(280);

        // Animação de Opacidade (Fade In) para suavizar a entrada
        var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
        fadeAnim.InsertKeyFrame(0f, 0f);
        fadeAnim.InsertKeyFrame(1f, 1f);
        fadeAnim.Duration = TimeSpan.FromMilliseconds(280);

        visual.StartAnimation("Translation", slideAnim);
        visual.StartAnimation("Opacity", fadeAnim);
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        var visual = ElementCompositionPreview.GetElementVisual(RootGrid);
        var compositor = visual.Compositor;

        var anim = compositor.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(0f, 1f);
        anim.InsertKeyFrame(1f, 0f);
        anim.Duration = TimeSpan.FromMilliseconds(200);

        visual.StartAnimation("Opacity", anim);

        await Task.Delay(200).ConfigureAwait(true);
        this.Close();
    }

    // --- Win32 Interop ---
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
