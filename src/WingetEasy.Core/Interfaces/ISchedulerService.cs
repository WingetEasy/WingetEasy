using System;
using System.Threading.Tasks;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;


/// <summary>
/// Contrato para o serviço de agendamento em segundo plano.
/// Determina quando o sistema deve "acordar" para procurar por novas atualizações de forma silenciosa.
/// </summary>


public interface ISchedulerService
{
    /// <summary>
    /// Evento disparado quando o temporizador atinge o momento exato de buscar atualizações.
    /// </summary>
    event EventHandler? CheckRequested;

    /// <summary>
    /// Inicia o temporizador carregando a configuração salva no banco de dados.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Atualiza a frequência do temporizador e guarda a configuração no banco de dados.
    /// </summary>
    Task UpdateScheduleAsync(ScheduleConfig config);

    /// <summary>
    /// Registra uma verificação bem-sucedida, resetando o contador de falhas e fechando o circuito.
    /// </summary>
    void RecordSuccess();

    /// <summary>
    /// Registra uma falha na verificação. Se atingir o limite, abre o circuito e pausa verificações automáticas.
    /// </summary>
    void RecordFailure();
}
