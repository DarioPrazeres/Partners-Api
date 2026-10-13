using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Partners.Core.Contracts;

namespace Partners.Tests.Support;

public static class ApiExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // NIFs únicos dentro do processo, acima de 100 000 000 (não colidem com NIFs fixos dos testes).
    private static long _nif = Random.Shared.NextInt64(100_000_000, 700_000_000);

    public static string NewNif() => Interlocked.Increment(ref _nif).ToString("D9");

    public static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    public static Task<HttpResponseMessage> PostPartner(
        this HttpClient client, string name, string taxNumber, string type = "CUSTOMER",
        string? email = null, decimal creditLimit = 100m) =>
        client.PostAsJsonAsync("/api/partners",
            new { Name = name, TaxNumber = taxNumber, Type = type, Email = email, CreditLimit = creditLimit });

    public static async Task<PartnerResponse> CreatePartner(
        this HttpClient client, string name, string? taxNumber = null, string type = "CUSTOMER", decimal creditLimit = 100m)
    {
        var response = await client.PostPartner(name, taxNumber ?? NewNif(), type, null, creditLimit);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<PartnerResponse>();
    }

    /// <summary>PUT completo; qualquer argumento omitido mantém o valor atual da entidade.</summary>
    public static Task<HttpResponseMessage> PutPartner(
        this HttpClient client, PartnerResponse current, string? name = null, string? taxNumber = null,
        string? type = null, string? email = null, decimal? creditLimit = null, bool? isActive = null,
        string? version = null) =>
        client.PutAsJsonAsync($"/api/partners/{current.Id}", new
        {
            Name = name ?? current.Name,
            TaxNumber = taxNumber ?? current.TaxNumber,
            Type = type ?? current.Type,
            Email = email ?? current.Email,
            CreditLimit = creditLimit ?? current.CreditLimit,
            IsActive = isActive ?? current.IsActive,
            Version = version ?? current.Version
        });

    public static async Task<PartnerResponse> GetPartner(this HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/partners/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PartnerResponse>();
    }

    public static async Task<PagedResponse<PartnerResponse>> ListPartners(this HttpClient client, string query)
    {
        var response = await client.GetAsync("/api/partners?" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResponse<PartnerResponse>>();
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Verifica o formato único de erro e que não há detalhes internos.</summary>
    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(code, doc.RootElement.GetProperty("code").GetString());
        Assert.Equal((int)status, doc.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("detail").GetString()));
        Assert.DoesNotContain("StackTrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", text);
    }
}
