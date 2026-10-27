namespace WingetEasy.Core.Models;

/// <summary>
/// Metadados opcionais fornecidos na execução de atualizações para enriquecer o histórico com versões e nomes reais.
/// </summary>
public sealed record PackageUpdateMetadata(string Name, string FromVersion, string ToVersion);

