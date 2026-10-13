namespace Partners.Core.Errors;

public sealed record FieldError(string Field, string Message);

public abstract class PartnersException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class RequestValidationException : PartnersException
{
    public RequestValidationException(IReadOnlyList<FieldError> errors)
        : base("validation_error", "Validation failed: " + string.Join(" ", errors.Select(e => e.Message)))
    {
        Errors = errors;
    }

    public IReadOnlyList<FieldError> Errors { get; }

    public static RequestValidationException BodyRequired() =>
        new(new[] { new FieldError("body", "A request body is required.") });
}

public sealed class PartnerNotFoundException(Guid id)
    : PartnersException("not_found", $"Partner '{id}' was not found.");

public sealed class DuplicateTaxNumberException(string taxNumber)
    : PartnersException("duplicate_tax_number", $"A partner with tax number '{taxNumber}' already exists.");

public sealed class VersionConflictException()
    : PartnersException("version_conflict",
        "The partner was modified by another request (or the Version is outdated). Fetch the latest data and retry.");
