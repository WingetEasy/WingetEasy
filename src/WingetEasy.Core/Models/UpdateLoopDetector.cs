using System;
using System.Collections.Generic;
using System.Linq;

namespace WingetEasy.Core.Models;

/// <summary>
/// Avalia se uma atualização pendente reportada pelo Winget deve ser suprimida, adiada
/// ou marcada como falso positivo recente (ex: pacotes com versão Unknown ou &lt; X que já foram atualizados).
/// </summary>
public static class UpdateLoopDetector
{
    public const string FalsePositiveReasonMessage =
        "Já atualizado recentemente (versão reportada incorretamente pelo fabricante)";

    public static readonly TimeSpan DefaultRecentWindow = TimeSpan.FromHours(48);

    /// <summary>
    /// Verifica se o pacote foi atualizado com sucesso para a mesma versão disponível dentro da janela recente (padrão 48h).
    /// Especialmente útil quando CurrentVersion é Unknown, começa com '&lt;' ou mantém formato divergente no Windows.
    /// </summary>
    public static bool IsRecentFalsePositive(
        WingetPackage package,
        IEnumerable<UpdateResult> recentHistory,
        DateTime nowUtc,
        TimeSpan? recentWindow = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(recentHistory);

        if (string.IsNullOrWhiteSpace(package.AvailableVersion))
        {
            return false;
        }

        var cutoffUtc = nowUtc - (recentWindow ?? DefaultRecentWindow);

        foreach (var entry in recentHistory)
        {
            if (!entry.Success)
                continue;

            if (!string.Equals(entry.PackageId, package.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            var updatedAtUtc = (entry.UpdatedAt ?? DateTime.MinValue).ToUniversalTime();
            if (updatedAtUtc < cutoffUtc)
                continue;

            // Se ToVersion foi gravada e coincide com a AvailableVersion oferecida (ou se está vazia mas a versão instalada é suspeita)
            if (!string.IsNullOrWhiteSpace(entry.ToVersion))
            {
                if (AreVersionsEquivalent(entry.ToVersion, package.AvailableVersion))
                {
                    return true;
                }
            }
            else if (IsSuspiciousInstalledVersion(package.CurrentVersion, package.AvailableVersion))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Identifica se a versão instalada reportada pelo Windows apresenta padrões conhecidos de falso positivo
    /// (ex: "Unknown", "Desconhecido", "&lt; 1.0", "&lt; 174.2.0", ou prefixo idêntico com sufixo ".0" omitido).
    /// </summary>
    public static bool IsSuspiciousInstalledVersion(string? currentVersion, string? availableVersion = null)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
            return true;

        var trimmed = currentVersion.Trim();
        if (trimmed.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Desconhecido", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Desconhecida", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith('<'))
        {
            return true;
        }

        // Verifica se a versão é menor que 1.0 (ex: 0.x) quando comparada ou isoladamente
        if (TryExtractNumericParts(trimmed, out var parts) && parts.Length > 0 && parts[0] == 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(availableVersion) && AreVersionsEquivalent(trimmed, availableVersion))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Verifica se uma regra de versão ignorada continua válida para a versão disponível atual.
    /// Retorna true se deve continuar suprimido (AvailableVersion &lt;= IgnoredVersion ou idêntica).
    /// Retorna false quando uma versão superior à ignorada foi lançada.
    /// </summary>
    public static bool ShouldSuppressForIgnoredVersion(string? ignoredVersion, string? availableVersion)
    {
        if (string.IsNullOrWhiteSpace(ignoredVersion) || string.IsNullOrWhiteSpace(availableVersion))
            return false;

        if (AreVersionsEquivalent(ignoredVersion, availableVersion))
            return true;

        // Se conseguirmos comparar numericamente e a disponível for maior que a ignorada, voltamos a avisar!
        if (CompareVersions(availableVersion, ignoredVersion) > 0)
            return false;

        return string.Equals(ignoredVersion.Trim(), availableVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compara se duas strings de versão representam a mesma versão (ex: "1.0.9015" vs "1.0.9015.0" ou "v1.0.9015").
    /// </summary>
    public static bool AreVersionsEquivalent(string? versionA, string? versionB)
    {
        if (string.IsNullOrWhiteSpace(versionA) || string.IsNullOrWhiteSpace(versionB))
            return false;

        var cleanA = NormalizeVersionString(versionA);
        var cleanB = NormalizeVersionString(versionB);

        if (string.Equals(cleanA, cleanB, StringComparison.OrdinalIgnoreCase))
            return true;

        if (TryExtractNumericParts(cleanA, out var partsA) && TryExtractNumericParts(cleanB, out var partsB))
        {
            var maxLen = Math.Max(partsA.Length, partsB.Length);
            for (int i = 0; i < maxLen; i++)
            {
                var valA = i < partsA.Length ? partsA[i] : 0;
                var valB = i < partsB.Length ? partsB[i] : 0;
                if (valA != valB)
                    return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Compara duas versões. Retorna &gt; 0 se versionA &gt; versionB, &lt; 0 se versionA &lt; versionB, 0 se equivalentes.
    /// </summary>
    public static int CompareVersions(string? versionA, string? versionB)
    {
        var cleanA = NormalizeVersionString(versionA ?? string.Empty);
        var cleanB = NormalizeVersionString(versionB ?? string.Empty);

        if (TryExtractNumericParts(cleanA, out var partsA) && TryExtractNumericParts(cleanB, out var partsB))
        {
            var maxLen = Math.Max(partsA.Length, partsB.Length);
            for (int i = 0; i < maxLen; i++)
            {
                var valA = i < partsA.Length ? partsA[i] : 0;
                var valB = i < partsB.Length ? partsB[i] : 0;
                if (valA != valB)
                    return valA.CompareTo(valB);
            }
            return 0;
        }

        return string.Compare(cleanA, cleanB, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersionString(string version)
    {
        var s = version.Trim();
        if (s.StartsWith("<=", StringComparison.Ordinal))
            s = s[2..].TrimStart();
        else if (s.StartsWith(">=", StringComparison.Ordinal))
            s = s[2..].TrimStart();
        else if (s.StartsWith('<') || s.StartsWith('>'))
            s = s[1..].TrimStart();

        if (s.StartsWith('v') || s.StartsWith('V'))
            s = s[1..].TrimStart();

        return s;
    }

    private static bool TryExtractNumericParts(string input, out int[] parts)
    {
        parts = [];
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Pega o prefixo numérico separado por pontos antes de qualquer sufixo (-beta, etc.)
        var mainPart = input.Split(['-', '+', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(mainPart))
            return false;

        var segments = mainPart.Split('.');
        var parsed = new List<int>(segments.Length);
        foreach (var seg in segments)
        {
            if (!int.TryParse(seg, out var num) || num < 0)
                return false;
            parsed.Add(num);
        }

        if (parsed.Count == 0)
            return false;

        parts = parsed.ToArray();
        return true;
    }
}

