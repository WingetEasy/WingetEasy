namespace WingetEasy.Core.Models;

/// <summary>
/// Códigos de saída (Exit Codes / HRESULT) oficiais do WinGet CLI.
/// Referência: https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md
/// </summary>
public static class WingetExitCodes
{
    public const int Success = 0;

    // ── Erros gerais do WinGet (0x8A1500xx) ─────────────────────────────

    /// <summary>0x8A150006 (-1978335226): O instalador retornou falha (ShellExecute).</summary>
    public const int ShellExecInstallFailed = unchecked((int)0x8A150006);

    /// <summary>0x8A150008 (-1978335224): Falha ao baixar o instalador.</summary>
    public const int DownloadFailed = unchecked((int)0x8A150008);

    /// <summary>0x8A150011 (-1978335215): Hash do instalador não corresponde ao manifesto.</summary>
    public const int InstallerHashMismatch = unchecked((int)0x8A150011);

    /// <summary>0x8A150014 (-1978335212): Nenhum pacote encontrado.</summary>
    public const int NoApplicationsFound = unchecked((int)0x8A150014);

    /// <summary>0x8A15002B (-1978335189): Nenhuma atualização aplicável encontrada.</summary>
    public const int NoApplicableUpdates = unchecked((int)0x8A15002B);

    /// <summary>0x8A150056 (-1978335146): O instalador não pode ser executado em contexto de administrador.</summary>
    public const int InstallerProhibitsElevation = unchecked((int)0x8A150056);

    /// <summary>0x8A150061 (-1978335135): Pacote já instalado (winget install).</summary>
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);

    // ── Erros retornados pelo instalador (0x8A1501xx) ───────────────────

    /// <summary>0x8A150101 (-1978334975): O aplicativo está em execução.</summary>
    public const int InstallPackageInUse = unchecked((int)0x8A150101);

    /// <summary>0x8A150102 (-1978334974): Outra instalação está em andamento.</summary>
    public const int InstallInProgress = unchecked((int)0x8A150102);

    /// <summary>0x8A150103 (-1978334973): Um ou mais arquivos estão em uso.</summary>
    public const int InstallFileInUse = unchecked((int)0x8A150103);

    /// <summary>0x8A150104 (-1978334972): Dependência ausente.</summary>
    public const int InstallMissingDependency = unchecked((int)0x8A150104);

    /// <summary>0x8A150105 (-1978334971): Disco cheio.</summary>
    public const int InstallDiskFull = unchecked((int)0x8A150105);

    /// <summary>0x8A150106 (-1978334970): Memória insuficiente.</summary>
    public const int InstallInsufficientMemory = unchecked((int)0x8A150106);

    /// <summary>0x8A150107 (-1978334969): Sem conexão de rede.</summary>
    public const int InstallNoNetwork = unchecked((int)0x8A150107);

    /// <summary>0x8A150108 (-1978334968): Erro do instalador; contate o suporte.</summary>
    public const int InstallContactSupport = unchecked((int)0x8A150108);

    /// <summary>0x8A150109 (-1978334967): Instalado com sucesso; reinicialização necessária para concluir.</summary>
    public const int InstallRebootRequiredToFinish = unchecked((int)0x8A150109);

    /// <summary>0x8A15010A (-1978334966): Reinicie o PC antes de instalar.</summary>
    public const int InstallRebootRequiredForInstall = unchecked((int)0x8A15010A);

    /// <summary>0x8A15010B (-1978334965): Instalado com sucesso; o instalador iniciou a reinicialização.</summary>
    public const int InstallRebootInitiated = unchecked((int)0x8A15010B);

    /// <summary>0x8A15010C (-1978334964): Instalação cancelada pelo usuário.</summary>
    public const int InstallCancelledByUser = unchecked((int)0x8A15010C);

    /// <summary>0x8A15010D (-1978334963): Outra versão já está instalada.</summary>
    public const int InstallAlreadyInstalled = unchecked((int)0x8A15010D);

    /// <summary>0x8A15010E (-1978334962): Uma versão mais nova já está instalada (downgrade).</summary>
    public const int InstallDowngrade = unchecked((int)0x8A15010E);

    /// <summary>0x8A15010F (-1978334961): Bloqueado por política da organização.</summary>
    public const int InstallBlockedByPolicy = unchecked((int)0x8A15010F);

    /// <summary>0x8A150110 (-1978334960): Falha ao instalar dependências.</summary>
    public const int InstallDependencies = unchecked((int)0x8A150110);

    /// <summary>0x8A150111 (-1978334959): O aplicativo está em uso por outro aplicativo.</summary>
    public const int InstallPackageInUseByApplication = unchecked((int)0x8A150111);

    /// <summary>0x8A150112 (-1978334958): Parâmetro inválido passado ao instalador.</summary>
    public const int InstallInvalidParameter = unchecked((int)0x8A150112);

    /// <summary>0x8A150113 (-1978334957): Sistema não suportado pelo pacote.</summary>
    public const int InstallSystemNotSupported = unchecked((int)0x8A150113);

    /// <summary>0x8A150114 (-1978334956): O instalador não suporta atualização de versão existente.</summary>
    public const int InstallUpgradeNotSupported = unchecked((int)0x8A150114);

    // ── Códigos do Windows / MSI frequentemente repassados pelo instalador ──

    /// <summary>5: ERROR_ACCESS_DENIED.</summary>
    public const int Win32AccessDenied = 5;

    /// <summary>0x80070005: E_ACCESSDENIED.</summary>
    public const int HResultAccessDenied = unchecked((int)0x80070005);

    /// <summary>740: ERROR_ELEVATION_REQUIRED.</summary>
    public const int Win32ElevationRequired = 740;

    /// <summary>0x800702E4: HRESULT de ERROR_ELEVATION_REQUIRED.</summary>
    public const int HResultElevationRequired = unchecked((int)0x800702E4);

    /// <summary>1641: ERROR_SUCCESS_REBOOT_INITIATED (MSI).</summary>
    public const int MsiRebootInitiated = 1641;

    /// <summary>3010: ERROR_SUCCESS_REBOOT_REQUIRED (MSI).</summary>
    public const int MsiRebootRequired = 3010;

    /// <summary>0x80070BC2: HRESULT de ERROR_SUCCESS_REBOOT_REQUIRED.</summary>
    public const int HResultRebootRequired = unchecked((int)0x80070BC2);

    /// <summary>
    /// Código canônico usado pelo WingetEasy para sinalizar "aplicativo em execução".
    /// Mantido por compatibilidade: aponta para o código oficial 0x8A150101.
    /// </summary>
    public const int AppRunning = InstallPackageInUse;
}
