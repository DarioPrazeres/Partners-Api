using System.Net;
using Microsoft.EntityFrameworkCore;
using Partners.Core.Contracts;
using Partners.Tests.Support;

namespace Partners.Tests;

/// <summary>
/// Cenário 5: pedidos simultâneos sobre SQL Server real. Cada pedido HTTP usa o seu próprio
/// DbContext/ligação; a verificação final usa um contexto independente.
/// </summary>
public class ConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Concurrent_creates_of_the_same_nif_yield_one_201_and_the_rest_409()
    {
        var nif = ApiExtensions.NewNif();
        var formats = new[]
        {
            nif,
            $"{nif[..3]}.{nif[3..6]}.{nif[6..]}",
            $"{nif[..3]} {nif[3..6]} {nif[6..]}",
            $"{nif[..3]}-{nif[3..6]}-{nif[6..]}"
        };

        const int attempts = 16;
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, attempts).Select(i => Task.Run(async () =>
        {
            await gate.Task;
            return await _client.PostPartner($"Corrida {i}", formats[i % formats.Length]);
        })).ToArray();

        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(attempts - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var stored = await factory.WithDbAsync(db => db.Partners.CountAsync(p => p.TaxNumber == nif));
        Assert.Equal(1, stored);
    }

    [Fact]
    public async Task Concurrent_updates_with_the_same_version_yield_one_200_and_the_rest_409()
    {
        var partner = await _client.CreatePartner("Antes da Corrida");

        const int attempts = 10;
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, attempts).Select(i => Task.Run(async () =>
        {
            await gate.Task;
            var name = $"Vencedor {i}";
            var response = await _client.PutPartner(partner, name: name); // todos com a mesma Version
            return (name, response);
        })).ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        var winners = results.Where(r => r.response.StatusCode == HttpStatusCode.OK).ToList();
        Assert.Single(winners);
        Assert.Equal(attempts - 1, results.Count(r => r.response.StatusCode == HttpStatusCode.Conflict));

        // O vencedor não foi sobrescrito por nenhum dos perdedores.
        var stored = await _client.GetPartner(partner.Id);
        Assert.Equal(winners[0].name, stored.Name);

        var winnerBody = await winners[0].response.ReadAsync<PartnerResponse>();
        Assert.Equal(winnerBody.Version, stored.Version);
    }

    [Fact]
    public async Task Concurrent_updates_to_the_same_new_nif_allow_only_one()
    {
        var a = await _client.CreatePartner("Candidato A");
        var b = await _client.CreatePartner("Candidato B");
        var target = ApiExtensions.NewNif();

        var gate = new TaskCompletionSource();
        var tasks = new[] { a, b }.Select(p => Task.Run(async () =>
        {
            await gate.Task;
            return await _client.PutPartner(p, taxNumber: target);
        })).ToArray();

        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await factory.WithDbAsync(db => db.Partners.CountAsync(p => p.TaxNumber == target)));
    }
}
