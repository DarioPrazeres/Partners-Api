using Microsoft.AspNetCore.Diagnostics;
using Partners.Core.Errors;

namespace Partners.Api.Errors;

public sealed class PartnersExceptionHandler(ILogger<PartnersExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            RequestValidationException e => ProblemFactory.Create(context, 400, e.Code, "Validation failed",
                "One or more fields are invalid.",
                e.Errors.Select(x => new ProblemError(x.Field, x.Message)).ToList()),

            PartnerNotFoundException e => ProblemFactory.Create(context, 404, e.Code, "Partner not found", e.Message),

            DuplicateTaxNumberException e => ProblemFactory.Create(context, 409, e.Code, "Duplicate tax number", e.Message),

            VersionConflictException e => ProblemFactory.Create(context, 409, e.Code, "Version conflict", e.Message),

            BadHttpRequestException => ProblemFactory.Create(context, 400, "malformed_request", "Malformed request",
                "The request could not be read."),

            _ => null
        };

        if (problem is null)
        {
            // Nunca expor detalhes internos nem stack traces ao cliente.
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            problem = ProblemFactory.Create(context, 500, "internal_error", "Internal server error",
                "An unexpected error occurred.");
        }

        await ProblemFactory.WriteAsync(context, problem, ct);
        return true;
    }
}
