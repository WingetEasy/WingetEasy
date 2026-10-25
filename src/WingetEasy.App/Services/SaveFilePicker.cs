using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WingetEasy.App.Services;

/// <summary>
/// Seletor de arquivo para salvar: usa o FileSavePicker do WinUI 3 (inicializado com o handle da janela)
/// e recai sobre o diálogo Win32 caso a API WinRT falhe (ex.: execução desempacotada).
/// </summary>
internal static class SaveFilePicker
{
    public static async Task<string?> PickAsync(
        IntPtr windowHandle,
        string fileType,
        string extension,
        string suggestedName,
        ILogger? logger = null)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = suggestedName,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeChoices.Add(fileType, [extension]);
            InitializeWithWindow.Initialize(picker, windowHandle);
            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "FileSavePicker indisponível; usando o diálogo Win32 como alternativa.");
            return Win32SaveFileDialog.Show(windowHandle, fileType, extension, suggestedName);
        }
    }
}

