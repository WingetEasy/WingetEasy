using System;

namespace WingetEasy.Core.Models;

public record ScheduleConfig(
    ScheduleFrequency Frequency,
    TimeSpan CheckTime,
    int[]? WeekDays = null // Usado para frequência semanal (0 = Domingo, 6 = Sábado)
);
