using Partners.Core.Contracts;
using Partners.Core.Domain;
using Partners.Core.Errors;
using Partners.Core.Persistence;

namespace Partners.Core.Services;

public sealed class PartnerService(IPartnerRepository repository, TimeProvider clock)
{
    public async Task<PartnerResponse> CreateAsync(CreatePartnerRequest? request, CancellationToken ct = default)
    {
        var data = PartnerValidator.ValidateCreate(request);
        var now = clock.GetUtcNow().UtcDateTime;

        var partner = new Partner
        {
            Id = Guid.CreateVersion7(),
            Name = data.Name,
            TaxNumber = data.TaxNumber,
            Type = data.Type,
            Email = data.Email,
            CreditLimit = data.CreditLimit,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        // A unicidade do NIF é garantida pelo índice único da base de dados.
        await repository.AddAsync(partner, ct);
        return PartnerMapper.ToResponse(partner);
    }

    public async Task<PartnerResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var partner = await repository.GetByIdAsync(id, ct) ?? throw new PartnerNotFoundException(id);
        return PartnerMapper.ToResponse(partner);
    }

    public async Task<PagedResponse<PartnerResponse>> ListAsync(PartnerListRequest? request, CancellationToken ct = default)
    {
        var criteria = PartnerValidator.ValidateList(request);
        var result = await repository.SearchAsync(criteria, ct);

        return new PagedResponse<PartnerResponse>
        {
            Items = result.Items.Select(PartnerMapper.ToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = criteria.Page,
            PageSize = criteria.PageSize
        };
    }

    public async Task<PartnerResponse> UpdateAsync(Guid id, UpdatePartnerRequest? request, CancellationToken ct = default)
    {
        var command = PartnerValidator.ValidateUpdate(request);
        var now = clock.GetUtcNow().UtcDateTime;

        var updated = await repository.UpdateAsync(id, command.Data, command.IsActive, command.ExpectedVersion, now, ct);
        return PartnerMapper.ToResponse(updated);
    }
}
