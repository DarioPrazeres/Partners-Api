namespace Partners.Core.Domain;

public enum PartnerType
{
    Customer = 1,
    Supplier = 2,
    Both = 3
}

public static class PartnerTypeCodes
{
    public const string Customer = "CUSTOMER";
    public const string Supplier = "SUPPLIER";
    public const string Both = "BOTH";

    public static string ToCode(PartnerType type) => type switch
    {
        PartnerType.Customer => Customer,
        PartnerType.Supplier => Supplier,
        PartnerType.Both => Both,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown partner type.")
    };

    public static PartnerType FromCode(string code) => code switch
    {
        Customer => PartnerType.Customer,
        Supplier => PartnerType.Supplier,
        Both => PartnerType.Both,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown partner type code.")
    };

    /// <summary>Aceita apenas CUSTOMER, SUPPLIER ou BOTH (sem distinguir maiúsculas). Números e outros textos são rejeitados.</summary>
    public static bool TryParse(string? value, out PartnerType type)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case Customer: type = PartnerType.Customer; return true;
            case Supplier: type = PartnerType.Supplier; return true;
            case Both: type = PartnerType.Both; return true;
            default: type = default; return false;
        }
    }
}
