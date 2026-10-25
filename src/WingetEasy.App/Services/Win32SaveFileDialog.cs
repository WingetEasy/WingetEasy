using System;
using System.Runtime.InteropServices;

namespace WingetEasy.App.Services;

/// <summary>
/// Fallback com o diálogo "Salvar como" clássico do Win32 (comdlg32), usado quando o FileSavePicker do WinRT falha.
/// </summary>
internal static class Win32SaveFileDialog
{
    private const int OfnOverwritePrompt = 0x00000002;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnPathMustExist = 0x00000800;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string? lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileNameW(ref OpenFileName ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

    /// <summary>
    /// Abre o diálogo "Abrir" e retorna o caminho escolhido, ou null se o usuário cancelar.
    /// </summary>
    public static string? ShowOpen(IntPtr owner, string fileTypeDescription, string extension)
    {
        const int bufferChars = 1024;
        var buffer = Marshal.AllocHGlobal(bufferChars * sizeof(char));
        try
        {
            Marshal.Copy(new char[bufferChars], 0, buffer, bufferChars);
            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = $"{fileTypeDescription} (*{extension})\0*{extension}\0\0",
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = bufferChars,
                lpstrInitialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Flags = OfnNoChangeDir | OfnPathMustExist | 0x00001000 // OFN_FILEMUSTEXIST
            };

            return GetOpenFileNameW(ref ofn) ? Marshal.PtrToStringUni(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
    /// <summary>
    /// Abre o diálogo e retorna o caminho escolhido, ou null se o usuário cancelar.
    /// </summary>
    public static string? Show(IntPtr owner, string fileTypeDescription, string extension, string suggestedName)
    {
        const int bufferChars = 1024;
        var buffer = Marshal.AllocHGlobal(bufferChars * sizeof(char));
        try
        {
            var initial = suggestedName + extension;
            var chars = new char[bufferChars];
            initial.CopyTo(0, chars, 0, Math.Min(initial.Length, bufferChars - 1));
            Marshal.Copy(chars, 0, buffer, bufferChars);

            var ext = extension.TrimStart('.');
            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = $"{fileTypeDescription} (*{extension})\0*{extension}\0\0",
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = bufferChars,
                lpstrInitialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                lpstrDefExt = ext,
                Flags = OfnOverwritePrompt | OfnNoChangeDir | OfnPathMustExist
            };

            return GetSaveFileNameW(ref ofn) ? Marshal.PtrToStringUni(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}

