using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Partners.Core.Contracts;
using Partners.Tests.Support;

namespace Partners.Tests;

public class PartnersApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Cenário 1
    [Fact]
    public async Task Create_normalizes_data_and_nif_stays_reserved_after_deactivation()
    {
        var response = await _client.PostPartner(" Ana Silva ", "001.234.567", "CUSTOMER", "  ana@example.com ", 2500.5m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<PartnerResponse>();
        Assert.Equal("Ana Silva", created.Name);
        Assert.Equal("001234567", created.TaxNumber);
        Assert.Equal("ana@example.com", created.Email);
        Assert.Equal("CUSTOMER", created.Type);
        Assert.Equal(2500.5m, created.CreditLimit);
        Assert.True(created.IsActive);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.False(string.IsNullOrEmpty(created.Version));
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.EndsWith($"/api/partners/{created.Id}", response.Headers.Location!.ToString());

        var viaLocation = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, viaLocation.StatusCode);

        var duplicate = await _client.PostPartner("Outra Ana", "001 234 567");
        await duplicate.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate_tax_number");

        var deactivated = await _client.PutPartner(created, isActive: false);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var stillDuplicate = await _client.PostPartner("Outra Ana", "001-234-567");
        await stillDuplicate.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate_tax_number");
    }

    [Fact]
    public async Task Create_blank_email_is_stored_as_null_and_boundaries_are_accepted()
    {
        var blank = await _client.PostPartner("Sem Email", NewNif(), "SUPPLIER", "   ", 0m);
        Assert.Equal(HttpStatusCode.Created, blank.StatusCode);
        Assert.Null((await blank.ReadAsync<PartnerResponse>()).Email);

        var max = await _client.PostPartner("Limite Maximo", NewNif(), "BOTH", null, 1_000_000m);
        Assert.Equal(HttpStatusCode.Created, max.StatusCode);
        Assert.Equal(1_000_000m, (await max.ReadAsync<PartnerResponse>()).CreditLimit);

        var trailingZero = await _client.PostPartner("Zero Extra", NewNif(), "CUSTOMER", null, 10.990m);
        Assert.Equal(HttpStatusCode.Created, trailingZero.StatusCode);
        Assert.Equal(10.99m, (await trailingZero.ReadAsync<PartnerResponse>()).CreditLimit);
    }

    // Cenário 2 + filtros combinados (cenário 7)
    [Fact]
    public async Task Type_filter_semantics_and_combined_filters()
    {
        var tok = NewToken();
        var customer = await _client.CreatePartner($"{tok} Cliente", type: "CUSTOMER");
        var supplier = await _client.CreatePartner($"{tok} Fornecedor", type: "SUPPLIER");
        var both = await _client.CreatePartner($"{tok} Ambos", type: "BOTH");
        var inactiveCustomer = await _client.CreatePartner($"{tok} Inativo", type: "CUSTOMER");
        Assert.Equal(HttpStatusCode.OK, (await _client.PutPartner(inactiveCustomer, isActive: false)).StatusCode);

        async Task<HashSet<Guid>> Ids(string extra)
        {
            var page = await _client.ListPartners($"Search={tok}&PageSize=100{extra}");
            return page.Items.Select(i => i.Id).ToHashSet();
        }

        // CUSTOMER inclui CUSTOMER e BOTH; SUPPLIER inclui SUPPLIER e BOTH; BOTH só BOTH
        Assert.Equal(new[] { customer.Id, both.Id }.ToHashSet(), await Ids("&Type=CUSTOMER"));
        Assert.Equal(new[] { supplier.Id, both.Id }.ToHashSet(), await Ids("&Type=SUPPLIER"));
        Assert.Equal(new[] { both.Id }.ToHashSet(), await Ids("&Type=BOTH"));

        // sem Type: todos os ativos; IncludeInactive junta o inativo; filtros combinam-se por AND
        Assert.Equal(new[] { customer.Id, supplier.Id, both.Id }.ToHashSet(), await Ids(""));
        Assert.Equal(new[] { customer.Id, supplier.Id, both.Id, inactiveCustomer.Id }.ToHashSet(), await Ids("&IncludeInactive=true"));
        Assert.Equal(new[] { customer.Id, both.Id, inactiveCustomer.Id }.ToHashSet(), await Ids("&Type=CUSTOMER&IncludeInactive=true"));
    }

    // Cenário 3
    [Fact]
    public async Task Deactivate_and_reactivate_via_put()
    {
        var tok = NewToken();
        var partner = await _client.CreatePartner($"{tok} Ciclo");

        var off = await _client.PutPartner(partner, isActive: false);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        var inactive = await off.ReadAsync<PartnerResponse>();
        Assert.False(inactive.IsActive);
        Assert.NotEqual(partner.Version, inactive.Version);

        Assert.Empty((await _client.ListPartners($"Search={tok}")).Items);
        Assert.Single((await _client.ListPartners($"Search={tok}&IncludeInactive=true")).Items);
        Assert.False((await _client.GetPartner(partner.Id)).IsActive);

        var on = await _client.PutPartner(inactive, isActive: true);
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.Single((await _client.ListPartners($"Search={tok}")).Items);
    }

    // Cenário 4
    [Fact]
    public async Task Update_keeping_own_nif_is_allowed_and_taking_anothers_returns_409_without_changes()
    {
        var a = await _client.CreatePartner("Parceiro A");
        var b = await _client.CreatePartner("Parceiro B");

        var ok = await _client.PutPartner(a, name: "  Parceiro A Renovado ", email: "a@example.com", creditLimit: 321.45m, type: "BOTH");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var updated = await ok.ReadAsync<PartnerResponse>();
        Assert.Equal("Parceiro A Renovado", updated.Name);
        Assert.Equal(a.TaxNumber, updated.TaxNumber);
        Assert.Equal("BOTH", updated.Type);
        Assert.Equal(a.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= a.UpdatedAt);
        Assert.NotEqual(a.Version, updated.Version);
        Assert.Equal(a.Id, updated.Id);

        var conflict = await _client.PutPartner(updated, name: "Nome Que Nao Deve Gravar", taxNumber: b.TaxNumber);
        await conflict.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate_tax_number");

        var after = await _client.GetPartner(a.Id);
        Assert.Equal("Parceiro A Renovado", after.Name);
        Assert.Equal(a.TaxNumber, after.TaxNumber);
        Assert.Equal(updated.Version, after.Version);
    }

    [Fact]
    public async Task Update_with_stale_version_returns_409_and_does_not_overwrite()
    {
        var p = await _client.CreatePartner("Versao Original");
        var first = await _client.PutPartner(p, name: "Primeira Alteracao");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await _client.PutPartner(p, name: "Alteracao Obsoleta"); // usa a Version antiga
        await stale.AssertProblemAsync(HttpStatusCode.Conflict, "version_conflict");

        Assert.Equal("Primeira Alteracao", (await _client.GetPartner(p.Id)).Name);
    }

    [Fact]
    public async Task Update_unknown_id_returns_404()
    {
        var ghost = new PartnerResponse
        {
            Id = Guid.NewGuid(), Name = "Fantasma", TaxNumber = NewNif(), Type = "CUSTOMER",
            CreditLimit = 1m, IsActive = true, Version = "AAAAAAAAAAA="
        };

        var response = await _client.PutPartner(ghost);
        await response.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_and_malformed_id_returns_400()
    {
        var missing = await _client.GetAsync($"/api/partners/{Guid.NewGuid()}");
        await missing.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        var malformed = await _client.GetAsync("/api/partners/not-a-guid");
        await malformed.AssertProblemAsync(HttpStatusCode.BadRequest, "malformed_request");
    }

    // Cenário 7: ordenação, TotalCount antes da paginação, desempate por Id
    [Fact]
    public async Task Pagination_counts_before_paging_and_breaks_name_ties_by_id()
    {
        var tok = NewToken();
        for (var i = 0; i < 5; i++) await _client.CreatePartner($"{tok} Same");

        var expected = await factory.WithDbAsync(db => db.Partners.AsNoTracking()
            .Where(p => p.Name.StartsWith(tok))
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync());
        Assert.Equal(5, expected.Count);

        var p1 = await _client.ListPartners($"Search={tok}&PageSize=2&Page=1");
        var p2 = await _client.ListPartners($"Search={tok}&PageSize=2&Page=2");
        var p3 = await _client.ListPartners($"Search={tok}&PageSize=2&Page=3");

        Assert.All(new[] { p1, p2, p3 }, p => Assert.Equal(5, p.TotalCount));
        Assert.Equal(new[] { 2, 2, 1 }, new[] { p1.Items.Count, p2.Items.Count, p3.Items.Count });
        Assert.Equal(expected, p1.Items.Concat(p2.Items).Concat(p3.Items).Select(i => i.Id).ToList());
        Assert.Equal((2, 3), (p2.PageSize, p3.Page));

        var beyond = await _client.ListPartners($"Search={tok}&PageSize=2&Page=99");
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
        Assert.Equal(99, beyond.Page);

        var huge = await _client.ListPartners($"Search={tok}&PageSize=100&Page=2147483647");
        Assert.Empty(huge.Items);
        Assert.Equal(5, huge.TotalCount);
    }

    [Fact]
    public async Task List_defaults_are_page_1_size_20()
    {
        var page = await _client.ListPartners("");
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    // Cenário 7: Search
    [Fact]
    public async Task Search_matches_name_case_insensitively_and_nif_with_separators()
    {
        var tok = NewToken();
        var ana = await _client.CreatePartner($"Ana {tok}");
        var bruno = await _client.CreatePartner($"Bruno {tok}");

        var byName = await _client.ListPartners($"Search={tok.ToUpperInvariant()}&PageSize=100");
        Assert.Equal(2, byName.TotalCount);

        var onlyAna = await _client.ListPartners($"Search=ANA {tok}");
        Assert.Equal(new[] { ana.Id }, onlyAna.Items.Select(i => i.Id));

        var nif = ana.TaxNumber;
        var formatted = $"{nif[..3]}.{nif[3..6]}-{nif[6..]}";
        var byNif = await _client.ListPartners($"Search={formatted}");
        Assert.Contains(byNif.Items, i => i.Id == ana.Id);
        Assert.DoesNotContain(byNif.Items, i => i.Id == bruno.Id);
    }

    [Fact]
    public async Task Search_ana_does_not_match_everything_through_the_nif_branch()
    {
        var tok = NewToken();
        await _client.CreatePartner($"Bruno {tok}");
        await _client.CreatePartner($"Carla {tok}");

        var result = await _client.ListPartners("Search=Ana&PageSize=100");
        Assert.All(result.Items, i => Assert.Contains("ana", i.Name, StringComparison.OrdinalIgnoreCase));

        // Pesquisa só com separadores: normalizada é vazia e não pode corresponder a todos os NIF.
        var separators = await _client.ListPartners("Search=...");
        Assert.Equal(0, separators.TotalCount);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        await _client.CreatePartner($"Parceiro {NewToken()}");

        Assert.Equal(0, (await _client.ListPartners("Search=%25")).TotalCount);   // '%'
        Assert.Equal(0, (await _client.ListPartners("Search=_")).TotalCount);     // '_'
    }

    // Cenário 6
    public static IEnumerable<object?[]> InvalidCreateBodies() => new[]
    {
        new object?[] { "Ana", "001234567", "CUSTOMER", null, "10.999" },
        new object?[] { "Ana", "12345678A", "CUSTOMER", null, "10" },
        new object?[] { "Ana", "12345678", "CUSTOMER", null, "10" },
        new object?[] { "Ana", "1234567890", "CUSTOMER", null, "10" },
        new object?[] { "Ana", "١٢٣٤٥٦٧٨٩", "CUSTOMER", null, "10" },
        new object?[] { "A", "123456789", "CUSTOMER", null, "10" },
        new object?[] { "   ", "123456789", "CUSTOMER", null, "10" },
        new object?[] { "Ana", "123456789", "INVALID", null, "10" },
        new object?[] { "Ana", "123456789", "1", null, "10" },
        new object?[] { "Ana", "123456789", "CUSTOMER", "not-an-email", "10" },
        new object?[] { "Ana", "123456789", "CUSTOMER", null, "-0.01" },
        new object?[] { "Ana", "123456789", "CUSTOMER", null, "1000000.01" },
    };

    [Theory]
    [MemberData(nameof(InvalidCreateBodies))]
    public async Task Create_with_invalid_data_returns_400_and_changes_nothing(
        string name, string nif, string type, string? email, string credit)
    {
        var before = await factory.CountPartnersAsync();

        var response = await _client.PostPartner(name, nif, type, email,
            decimal.Parse(credit, System.Globalization.CultureInfo.InvariantCulture));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_error");
        Assert.Equal(before, await factory.CountPartnersAsync());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"Name\":\"Ana\",\"TaxNumber\":\"123456789\",\"Type\":\"CUSTOMER\",\"CreditLimit\":\"abc\"}")]
    [InlineData("{\"Name\":123,\"TaxNumber\":\"123456789\",\"Type\":\"CUSTOMER\",\"CreditLimit\":1}")]
    [InlineData("{}")]
    public async Task Create_with_malformed_or_incomplete_body_returns_400(string body)
    {
        var before = await factory.CountPartnersAsync();

        var response = await _client.PostAsync("/api/partners", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(before, await factory.CountPartnersAsync());
    }

    [Theory]
    [InlineData("Page=0")]
    [InlineData("Page=-1")]
    [InlineData("Page=abc")]
    [InlineData("PageSize=0")]
    [InlineData("PageSize=101")]
    [InlineData("Type=INVALID")]
    [InlineData("IncludeInactive=maybe")]
    public async Task List_with_invalid_filters_returns_400(string query)
    {
        var response = await _client.GetAsync("/api/partners?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-base64!")]
    [InlineData("AAAA")]
    public async Task Update_with_missing_or_malformed_version_returns_400_and_changes_nothing(string? version)
    {
        var p = await _client.CreatePartner("Antes");

        var response = await _client.PutAsJsonAsync($"/api/partners/{p.Id}", new
        {
            Name = "Depois", TaxNumber = p.TaxNumber, Type = "CUSTOMER", Email = (string?)null,
            CreditLimit = 5m, IsActive = true, Version = version
        });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_error");
        var after = await _client.GetPartner(p.Id);
        Assert.Equal("Antes", after.Name);
        Assert.Equal(p.Version, after.Version);
    }

    [Fact]
    public async Task Invalid_update_does_not_partially_change_the_record()
    {
        var p = await _client.CreatePartner("Intacto", creditLimit: 50m);

        var response = await _client.PutPartner(p, name: "Mudou", creditLimit: 10.999m);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_error");
        var after = await _client.GetPartner(p.Id);
        Assert.Equal("Intacto", after.Name);
        Assert.Equal(50m, after.CreditLimit);
        Assert.Equal(p.Version, after.Version);
    }

    [Fact]
    public async Task Put_requires_all_fields_but_email_may_be_null()
    {
        var p = await _client.CreatePartner("Campos Completos");

        var missing = await _client.PutAsJsonAsync($"/api/partners/{p.Id}", new { Name = "So Nome", Version = p.Version });
        await missing.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_error");

        var nullEmail = await _client.PutAsJsonAsync($"/api/partners/{p.Id}", new
        {
            Name = "Campos Completos", TaxNumber = p.TaxNumber, Type = "CUSTOMER", Email = (string?)null,
            CreditLimit = 100m, IsActive = true, Version = p.Version
        });
        Assert.Equal(HttpStatusCode.OK, nullEmail.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_set_id_or_dates()
    {
        var forgedId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync("/api/partners", new
        {
            Id = forgedId, CreatedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Name = "Forjado", TaxNumber = NewNif(), Type = "CUSTOMER", CreditLimit = 1m
        });

        var created = await response.ReadAsync<PartnerResponse>();
        Assert.NotEqual(forgedId, created.Id);
        Assert.True(created.CreatedAt.Year >= 2026);
    }

    private static string NewNif() => ApiExtensions.NewNif();
    private static string NewToken() => ApiExtensions.NewToken();
}
