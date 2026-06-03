using System;
using WingetEasy.Core.Models;

namespace WingetEasy.Core.Interfaces;

/// <summary>
/// Gera o agendamento de tarefas em segundo plano (background).
/// Determina quando o sistema deve "acordar" para procurar por novas atualizações de forma silenciosa.
/// </summary>

public interface IschedulerService
{
    void Start();
    void Stop();
    void UpdateSchedule(ScheduleConfig config);
    event EventHandler? CheckRequested;
}
