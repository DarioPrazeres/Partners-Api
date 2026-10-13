using Partners.Core.Contracts;
using Partners.Core.Domain;

namespace Partners.Core.Services;

public static class PartnerMapper
{
    public static PartnerResponse ToResponse(Partner p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        TaxNumber = p.TaxNumber,
        Type = PartnerTypeCodes.ToCode(p.Type),
        Email = p.Email,
        CreditLimit = p.CreditLimit,
        IsActive = p.IsActive,
        Version = Convert.ToBase64String(p.RowVersion),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
