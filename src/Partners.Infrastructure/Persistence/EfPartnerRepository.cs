using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Partners.Core.Domain;
using Partners.Core.Errors;
using Partners.Core.Persistence;

namespace Partners.Infrastructure.Persistence;

public sealed class EfPartnerRepository(PartnersDbContext db) : IPartnerRepository
{
    public async Task AddAsync(Partner partner, CancellationToken ct = default)
    {
        db.Partners.Add(partner);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new DuplicateTaxNumberException(partner.TaxNumber);
        }
    }

    public Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PagedResult<Partner>> SearchAsync(PartnerSearchCriteria c, CancellationToken ct = default)
    {
        IQueryable<Partner> query = db.Partners.AsNoTracking();

        if (!c.IncludeInactive)
            query = query.Where(p => p.IsActive);

        // CUSTOMER inclui BOTH; SUPPLIER inclui BOTH; BOTH inclui apenas BOTH.
        switch (c.Type)
        {
            case PartnerType.Customer:
                query = query.Where(p => p.Type == PartnerType.Customer || p.Type == PartnerType.Both);
                break;
            case PartnerType.Supplier:
                query = query.Where(p => p.Type == PartnerType.Supplier || p.Type == PartnerType.Both);
                break;
            case PartnerType.Both:
                query = query.Where(p => p.Type == PartnerType.Both);
                break;
        }

        if (c.NameTerm is not null)
        {
            var namePattern = "%" + EscapeLike(c.NameTerm) + "%";
            if (c.TaxTerm is not null)
            {
                var taxPattern = "%" + EscapeLike(c.TaxTerm) + "%";
                query = query.Where(p =>
                    EF.Functions.Like(p.Name, namePattern, "\\") ||
                    EF.Functions.Like(p.TaxNumber, taxPattern, "\\"));
            }
            else
            {
                query = query.Where(p => EF.Functions.Like(p.Name, namePattern, "\\"));
            }
        }

        // Conta antes de paginar; filtros e paginação executam no SQL Server.
        var total = await query.CountAsync(ct);

        var offset = (long)(c.Page - 1) * c.PageSize;
        if (offset >= total)
            return new PagedResult<Partner>(Array.Empty<Partner>(), total);

        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((int)offset)
            .Take(c.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Partner>(items, total);
    }

    public async Task<Partner> UpdateAsync(
        Guid id, PartnerData data, bool isActive, byte[] expectedVersion, DateTime nowUtc, CancellationToken ct = default)
    {
        var entity = await db.Partners.FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw new PartnerNotFoundException(id);

        // A versão do cliente passa a ser o "valor original" do token de concorrência:
        // o UPDATE gerado é "... WHERE Id = @id AND RowVersion = @versao" e a comparação
        // é feita pelo SQL Server. Se ninguém for afetado, EF lança DbUpdateConcurrencyException.
        db.Entry(entity).Property(p => p.RowVersion).OriginalValue = expectedVersion;

        entity.Name = data.Name;
        entity.TaxNumber = data.TaxNumber;
        entity.Type = data.Type;
        entity.Email = data.Email;
        entity.CreditLimit = data.CreditLimit;
        entity.IsActive = isActive;
        entity.UpdatedAt = nowUtc; // muda sempre, logo o UPDATE (e a verificação de versão) é sempre executado

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new VersionConflictException();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new DuplicateTaxNumberException(data.TaxNumber);
        }

        return entity;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
