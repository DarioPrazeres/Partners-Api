using Partners.Core.Domain;

namespace Partners.Core.Persistence;

public sealed record PartnerData(string Name, string TaxNumber, PartnerType Type, string? Email, decimal CreditLimit);

/// <param name="NameTerm">Texto a procurar no nome (null = sem pesquisa).</param>
/// <param name="TaxTerm">Texto normalizado a procurar no NIF; null quando a pesquisa normalizada fica vazia (nunca corresponde a tudo).</param>
public sealed record PartnerSearchCriteria(
    string? NameTerm,
    string? TaxTerm,
    PartnerType? Type,
    bool IncludeInactive,
    int Page,
    int PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public interface IPartnerRepository
{
    /// <exception cref="Errors.DuplicateTaxNumberException"/>
    Task AddAsync(Partner partner, CancellationToken ct = default);

    Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<Partner>> SearchAsync(PartnerSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>Atualiza de forma atómica, com verificação da versão feita pela base de dados.</summary>
    /// <exception cref="Errors.PartnerNotFoundException"/>
    /// <exception cref="Errors.VersionConflictException"/>
    /// <exception cref="Errors.DuplicateTaxNumberException"/>
    Task<Partner> UpdateAsync(
        Guid id,
        PartnerData data,
        bool isActive,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken ct = default);
}
