namespace Partners.Core.Domain;

public class Partner
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>NIF normalizado: exatamente nove dígitos ASCII, guardado como texto.</summary>
    public string TaxNumber { get; set; } = string.Empty;

    public PartnerType Type { get; set; }
    public string? Email { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Token de concorrência (rowversion do SQL Server). Exposto à API em Base64 como "Version".</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
