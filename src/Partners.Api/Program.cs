using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Partners.Api.Errors;
using Partners.Core.Services;
using Partners.Infrastructure;
using Partners.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    // Respostas em PascalCase (Items, TotalCount, Page, PageSize, Version...), tal como no enunciado.
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null)
    .ConfigureApiBehaviorOptions(o =>
    {
        // JSON inválido, GUID/inteiros/booleanos malformados -> 400 no mesmo formato dos restantes erros.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(e => e.Value is { Errors.Count: > 0 })
                .SelectMany(e => e.Value!.Errors.Select(err => new ProblemError(CleanKey(e.Key), DescribeError(err))))
                .ToList();

            var problem = ProblemFactory.Create(ctx.HttpContext, StatusCodes.Status400BadRequest, "malformed_request",
                "Malformed request", "The request is malformed or contains values of the wrong type.", errors);
            return ProblemFactory.ToActionResult(problem);
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PartnersExceptionHandler>();
builder.Services.AddOpenApi(); 

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PartnerService>();
builder.Services.AddPartnersInfrastructure(sp =>
    sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."));

var app = builder.Build();

var isDev = app.Environment.IsDevelopment();
var migrate = isDev || app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
var seed = isDev || app.Configuration.GetValue<bool>("Database:Seed");
if (migrate || seed)
    await DatabaseInitializer.InitializeAsync(app.Services, migrate, seed);

app.UseExceptionHandler();
app.UseStatusCodePages(); // 404/405/415... sem corpo passam a usar problem+json
if (isDev)
{
    app.MapOpenApi(); 

    // Swagger UI carregado por CDN: evita depender de mais um pacote NuGet
    app.MapGet("/swagger", () => Results.Content("""
        <!doctype html>
        <html><head><title>Partners API</title>
        <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css" />
        </head><body><div id="ui"></div>
        <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
        <script>SwaggerUIBundle({ url: '/openapi/v1.json', dom_id: '#ui' });</script>
        </body></html>
        """, "text/html"));
}
app.MapControllers();

app.Run();

static string CleanKey(string key)
{
    if (string.IsNullOrEmpty(key) || key == "$") return "body";
    return key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
}

static string DescribeError(Microsoft.AspNetCore.Mvc.ModelBinding.ModelError error)
{
    if (error.Exception is JsonException)
        return "The request body is not valid JSON or a field has an invalid type.";
    return string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage;
}

// Necessário para WebApplicationFactory<Program> nos testes de integração.
public partial class Program { }
