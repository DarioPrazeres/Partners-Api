using System.Text;

namespace Partners.Core.Domain;

/// <summary>Regra simplificada do exercício: sem validação fiscal real.</summary>
public static class TaxNumberRules
{
    public const int Length = 9;

    /// <summary>Remove espaços (qualquer whitespace), pontos e hífenes.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || c == '.' || c == '-') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Exatamente nove dígitos ASCII (0-9). Dígitos Unicode de outros alfabetos são rejeitados.</summary>
    public static bool IsValid(string normalized)
    {
        if (normalized.Length != Length) return false;
        foreach (var c in normalized)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }
}
