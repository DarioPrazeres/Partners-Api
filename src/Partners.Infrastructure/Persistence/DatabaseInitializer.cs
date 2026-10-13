using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Partners.Core.Domain;

namespace Partners.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool migrate, bool seed, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PartnersDbContext>();

        if (migrate)
            await db.Database.MigrateAsync(ct);

        if (seed && !await db.Partners.AnyAsync(ct))
        {
            db.Partners.AddRange(SampleData.Create(DateTime.UtcNow));
            await db.SaveChangesAsync(ct);
        }
    }
}

public static class SampleData
{
    public static readonly Guid AnaSilvaId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IReadOnlyList<Partner> Create(DateTime nowUtc)
    {
        Partner P(string id, string name, string nif, PartnerType type, string? email, decimal credit, bool active = true) => new()
        {
            Id = Guid.Parse(id),
            Name = name,
            TaxNumber = nif,
            Type = type,
            Email = email,
            CreditLimit = credit,
            IsActive = active,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        var list = new List<Partner>
    {
        P("11111111-1111-1111-1111-111111111111", "Ana Silva", "001234567", PartnerType.Customer, "ana.silva@example.com", 5000m),
        P("22222222-2222-2222-2222-222222222222", "Ana Silva", "123456789", PartnerType.Customer, null, 1000m),
        P("33333333-3333-3333-3333-333333333333", "Comercial Horizonte Lda", "500123456", PartnerType.Supplier, "geral@horizonte.example", 0m),
        P("44444444-4444-4444-4444-444444444444", "Tecnologias Ponte SA", "501234567", PartnerType.Both, "contacto@ponte.example", 25000.50m),
        P("55555555-5555-5555-5555-555555555555", "Mercearia do Bairro", "234567891", PartnerType.Customer, null, 750m, active: false),
        P("66666666-6666-6666-6666-666666666666", "Distribuidora Atlântico", "345678912", PartnerType.Supplier, "vendas@atlantico.example", 100000m),
        P("77777777-7777-7777-7777-777777777777", "Anabela Costa", "456789123", PartnerType.Both, "anabela@example.com", 12000m),
        P("88888888-8888-8888-8888-888888888888", "Oficina Central", "567891234", PartnerType.Customer, null, 300m)
    };

        var extras = new (string Name, PartnerType Type, string? Email, decimal Credit, bool Active)[]
        {
        ("Maria Santos", PartnerType.Customer, "maria.santos@example.com", 1500m, true),
        ("Maria Santos", PartnerType.Customer, null, 800m, true),
        ("Maria Santos", PartnerType.Both, "m.santos@empresa.example", 9000m, true),
        ("João Fernandes", PartnerType.Customer, "joao.fernandes@example.com", 2200.75m, true),
        ("Pedro Domingos", PartnerType.Supplier, null, 0m, true),
        ("Luanda Fresh Lda", PartnerType.Supplier, "vendas@luandafresh.example", 45000m, true),
        ("Benguela Transportes SA", PartnerType.Supplier, "geral@benguelatransportes.example", 150000m, true),
        ("Huambo Construções Lda", PartnerType.Both, "obras@huambo.example", 300000m, true),
        ("Lobito Pescas", PartnerType.Supplier, null, 12000m, false),
        ("Kwanza Papelaria", PartnerType.Customer, "papelaria@kwanza.example", 600m, true),
        ("Cabinda Farma", PartnerType.Customer, "compras@cabindafarma.example", 8500m, true),
        ("Sofia Martins", PartnerType.Customer, "sofia.martins@example.com", 1000m, true),
        ("Tiago Neto", PartnerType.Customer, null, 0m, true),
        ("Ngola Informática", PartnerType.Both, "info@ngolainformatica.example", 60000m, true),
        ("Mussulo Turismo", PartnerType.Customer, "reservas@mussulo.example", 20000m, true),
        ("Zango Materiais", PartnerType.Supplier, "zango@materiais.example", 35000m, true),
        ("Beatriz Gomes", PartnerType.Customer, "beatriz.gomes@example.com", 400m, false),
        ("Carlos Mendes", PartnerType.Both, "carlos.mendes@example.com", 7500m, true),
        ("Kilamba Serviços", PartnerType.Supplier, "contacto@kilamba.example", 90000m, true),
        ("Talatona Restauração", PartnerType.Customer, "geral@talatona.example", 5000m, true),
        ("Viana Logística", PartnerType.Both, "ops@vianalogistica.example", 120000m, true),
        ("Ana Paula Ferreira", PartnerType.Customer, "anapaula@example.com", 950m, true),
        ("Rui Baptista", PartnerType.Customer, null, 2500m, false),
        ("Cacuaco Agro Lda", PartnerType.Supplier, "agro@cacuaco.example", 18000m, true)
        };

        for (var i = 0; i < extras.Length; i++)
        {
            var e = extras[i];
            list.Add(new Partner
            {
                Id = Guid.CreateVersion7(),
                Name = e.Name,
                TaxNumber = (600_000_001 + i * 7919).ToString("D9"), // 9 dígitos, únicos
                Type = e.Type,
                Email = e.Email,
                CreditLimit = e.Credit,
                IsActive = e.Active,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            });
        }

        return list;
    }
}
