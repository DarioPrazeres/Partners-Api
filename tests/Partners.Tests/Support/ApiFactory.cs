using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Partners.Infrastructure.Persistence;

namespace Partners.Tests.Support;

/// Cada fixture cria a sua base (PartnersTests_<guid>), aplica as migrações e apaga-a no fim.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = "PartnersTests_" + Guid.NewGuid().ToString("N");
    private string _connectionString = string.Empty;

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            }));
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        var server = await SqlServerHost.GetServerConnectionAsync();
        _connectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = _databaseName,
            TrustServerCertificate = true
        }.ConnectionString;

        using var scope = Services.CreateScope(); // só agora a API arranca, já com a ligação certa
        var db = scope.ServiceProvider.GetRequiredService<PartnersDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_connectionString.Length == 0) return;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PartnersDbContext>();
        await db.Database.EnsureDeletedAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<PartnersDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<PartnersDbContext>());
    }

    public Task<int> CountPartnersAsync() => WithDbAsync(db => db.Partners.CountAsync());
}