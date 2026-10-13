using Testcontainers.MsSql;

namespace Partners.Tests.Support;

/// <summary>
/// Fornece a ligação ao SQL Server dos testes de integração, uma vez por execução:
/// 1) PARTNERS_TEST_CONNECTION, se definida (sem Database); 2) caso contrário, sobe SQL Server 2022 em Docker.
/// </summary>
public static class SqlServerHost
{
    private static readonly Lazy<Task<string>> Server = new(StartAsync);

    public static Task<string> GetServerConnectionAsync() => Server.Value;

    private static async Task<string> StartAsync()
    {
        var fromEnv = Environment.GetEnvironmentVariable("PARTNERS_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        try
        {
            // O contentor é removido automaticamente no fim (Ryuk, do Testcontainers).
            var container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
            await container.StartAsync();
            return container.GetConnectionString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Os testes de integração precisam de SQL Server. Abra o Docker Desktop e volte a correr " +
                "'dotnet test', ou defina PARTNERS_TEST_CONNECTION (ver README).", ex);
        }
    }
}