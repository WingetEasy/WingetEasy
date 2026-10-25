using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WingetEasy.App.Services;

/// <summary>
/// Seletor de arquivo para abrir: FileOpenPicker do WinUI 3 com o handle da janela e fallback para o diálogo Win32.
/// </summary>
internal static class OpenFilePicker
{
    public static async Task<string?> PickAsync(IntPtr windowHandle, string fileType, string extension, ILogger? logger = null)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(extension);
            InitializeWithWindow.Initialize(picker, windowHandle);
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "FileOpenPicker indisponível; usando o diálogo Win32 como alternativa.");
            return Win32SaveFileDialog.ShowOpen(windowHandle, fileType, extension);
        }
    }
}