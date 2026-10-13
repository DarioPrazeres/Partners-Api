namespace Partners.Core.Contracts;

// Todos os campos do pedido são anuláveis para que a validação (e não o model binding)
// produza mensagens consistentes para campos em falta.

public sealed class CreatePartnerRequest
{
    public string? Name { get; set; }
    public string? TaxNumber { get; set; }
    public string? Type { get; set; }
    public string? Email { get; set; }
    public decimal? CreditLimit { get; set; }
}

public sealed class UpdatePartnerRequest
{
    public string? Name { get; set; }
    public string? TaxNumber { get; set; }
    public string? Type { get; set; }
    public string? Email { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool? IsActive { get; set; }
    public string? Version { get; set; }
}

public sealed class PartnerListRequest
{
    public string? Search { get; set; }
    public string? Type { get; set; }
    public bool IncludeInactive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class PartnerResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TaxNumber { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Email { get; init; }
    public decimal CreditLimit { get; init; }
    public bool IsActive { get; init; }
    public string Version { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
