using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Serviço responsável por identificar processos ativos no sistema associados aos pacotes do WinGet,
/// verificar existência de janelas visíveis e solicitar fechamento seguro ou forçado.
/// </summary>
public interface IRunningProcessService
{
    /// <summary>
    /// Verifica se qualquer processo associado ao pacote está em execução no momento.
    /// </summary>
    bool IsAppRunning(string packageId, string? packageName = null);

    /// <summary>
    /// Verifica se o aplicativo está em execução com janela visível para o usuário
    /// (ex.: navegador aberto, chamada ativa, editor com arquivos).
    /// </summary>
    bool IsAppRunningWithVisibleWindow(string packageId, string? packageName = null);

    /// <summary>
    /// Retorna os nomes dos processos ativos associados ao pacote.
    /// </summary>
    IReadOnlyList<string> GetRunningProcessNames(string packageId, string? packageName = null);

    /// <summary>
    /// Solicita o fechamento dos processos do aplicativo de forma graciosa e,
    /// opcionalmente, força encerramento se não responder dentro do tempo limite.
    /// </summary>
    Task<bool> CloseAppProcessesAsync(string packageId, string? packageName = null, bool killIfUnresponsive = true, TimeSpan? timeout = null);
}

