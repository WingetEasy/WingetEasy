namespace WingetEasy.Core.Models;

public enum PackageCategory
{
    App,
    Runtime,
    Security,
    Driver,
    Font,
    Other
}

public enum ScheduleFrequency
{
    Manual, // Desabilitado
    TwiceDaily, // 2x por dia a cada 12 horas
    OnceDaily, // 1x por dia a cada 24 horas
    Weekly // 1x por semana a cada 7 dias
}

public enum AppTheme
{
    System,
    Light,
    Dark
}
