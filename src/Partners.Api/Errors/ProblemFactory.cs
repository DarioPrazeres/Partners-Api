using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Partners.Api.Errors;

public sealed record ProblemError(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("message")] string Message);

/// <summary>Formato único de erro (application/problem+json) para todos os caminhos de falha.</summary>
public static class ProblemFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(
        HttpContext context, int status, string code, string title, string detail,
        IReadOnlyCollection<ProblemError>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (errors is { Count: > 0 }) problem.Extensions["errors"] = errors;
        return problem;
    }

    public static async Task WriteAsync(HttpContext context, ProblemDetails problem, CancellationToken ct = default)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions), ct);
    }

    public static IActionResult ToActionResult(ProblemDetails problem) => new ContentResult
    {
        StatusCode = problem.Status,
        ContentType = "application/problem+json",
        Content = JsonSerializer.Serialize(problem, JsonOptions)
    };
}
