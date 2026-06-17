using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace WingetEasy.App
{
    public sealed partial class MainWindow : Window
    {
        private AppWindow _appWindow;

        public MainWindow()
        {
            this.InitializeComponent();

            // Intercepta o comportamento nativo da janela do Windows
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            // Subscreve ao evento de fecho (clique no "X")
            _appWindow.Closing += AppWindow_Closing;
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            // Cancela a destruição real da janela
            args.Cancel = true;

            // Esconde a janela do ecrã (Minimiza para a bandeja)
            sender.Hide();
        }
    }
}
