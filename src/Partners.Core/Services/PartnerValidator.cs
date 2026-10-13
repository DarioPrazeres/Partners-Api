using System.Text.RegularExpressions;
using Partners.Core.Contracts;
using Partners.Core.Domain;
using Partners.Core.Errors;
using Partners.Core.Persistence;

namespace Partners.Core.Services;

public sealed record PartnerUpdateCommand(PartnerData Data, bool IsActive, byte[] ExpectedVersion);

/// <summary>Regras de validação e normalização. Não depende de infraestrutura.</summary>
public static partial class PartnerValidator
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const decimal CreditLimitMax = 1_000_000m;
    public const int MaxPageSize = 100;
    public const int VersionByteLength = 8;

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    public static PartnerData ValidateCreate(CreatePartnerRequest? request)
    {
        if (request is null) throw RequestValidationException.BodyRequired();

        var errors = new List<FieldError>();
        var data = ValidateData(request.Name, request.TaxNumber, request.Type, request.Email, request.CreditLimit, errors);
        if (errors.Count > 0 || data is null) throw new RequestValidationException(errors);
        return data;
    }

    public static PartnerUpdateCommand ValidateUpdate(UpdatePartnerRequest? request)
    {
        if (request is null) throw RequestValidationException.BodyRequired();

        var errors = new List<FieldError>();
        var data = ValidateData(request.Name, request.TaxNumber, request.Type, request.Email, request.CreditLimit, errors);

        if (request.IsActive is null) errors.Add(new FieldError("IsActive", "IsActive is required."));

        byte[]? version = null;
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            errors.Add(new FieldError("Version", "Version is required."));
        }
        else if (!TryParseVersion(request.Version, out var parsed))
        {
            errors.Add(new FieldError("Version", "Version is malformed. Use the opaque value returned by the API."));
        }
        else
        {
            version = parsed;
        }

        if (errors.Count > 0 || data is null || version is null) throw new RequestValidationException(errors);
        return new PartnerUpdateCommand(data, request.IsActive!.Value, version);
    }

    public static PartnerSearchCriteria ValidateList(PartnerListRequest? request)
    {
        request ??= new PartnerListRequest();
        var errors = new List<FieldError>();

        if (request.Page < 1) errors.Add(new FieldError("Page", "Page must be greater than or equal to 1."));
        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
            errors.Add(new FieldError("PageSize", $"PageSize must be between 1 and {MaxPageSize}."));

        PartnerType? type = null;
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            if (PartnerTypeCodes.TryParse(request.Type, out var parsed)) type = parsed;
            else errors.Add(new FieldError("Type", "Type must be CUSTOMER, SUPPLIER or BOTH."));
        }

        if (errors.Count > 0) throw new RequestValidationException(errors);

        string? nameTerm = null;
        string? taxTerm = null;
        var search = request.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            nameTerm = search;
            // Uma pesquisa só com separadores normaliza para vazio: nesse caso o ramo do NIF
            // é desligado (nunca "contém vazio", que corresponderia a tudo).
            var normalized = TaxNumberRules.Normalize(search);
            if (normalized.Length > 0) taxTerm = normalized;
        }

        return new PartnerSearchCriteria(nameTerm, taxTerm, type, request.IncludeInactive, request.Page, request.PageSize);
    }

    /// <summary>Version é Base64 canónico de exatamente 8 bytes (rowversion).</summary>
    public static bool TryParseVersion(string? raw, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrEmpty(raw)) return false;

        var buffer = new byte[VersionByteLength];
        if (!Convert.TryFromBase64String(raw, buffer, out var written) || written != VersionByteLength) return false;
        if (Convert.ToBase64String(buffer) != raw) return false;

        bytes = buffer;
        return true;
    }

    private static PartnerData? ValidateData(
        string? rawName, string? rawTaxNumber, string? rawType, string? rawEmail, decimal? rawCredit,
        List<FieldError> errors)
    {
        var before = errors.Count;

        var name = rawName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors.Add(new FieldError("Name", "Name is required."));
        }
        else if (name.Length < NameMinLength || name.Length > NameMaxLength)
        {
            errors.Add(new FieldError("Name", $"Name must have between {NameMinLength} and {NameMaxLength} characters."));
        }

        var taxNumber = TaxNumberRules.Normalize(rawTaxNumber);
        if (taxNumber.Length == 0)
        {
            errors.Add(new FieldError("TaxNumber", "TaxNumber is required."));
        }
        else if (!TaxNumberRules.IsValid(taxNumber))
        {
            errors.Add(new FieldError("TaxNumber",
                "TaxNumber must contain exactly nine digits (0-9) after removing spaces, dots and hyphens."));
        }

        var type = default(PartnerType);
        if (string.IsNullOrWhiteSpace(rawType))
        {
            errors.Add(new FieldError("Type", "Type is required (CUSTOMER, SUPPLIER or BOTH)."));
        }
        else if (!PartnerTypeCodes.TryParse(rawType, out type))
        {
            errors.Add(new FieldError("Type", "Type must be CUSTOMER, SUPPLIER or BOTH."));
        }

        string? email = null;
        var trimmedEmail = rawEmail?.Trim();
        if (!string.IsNullOrEmpty(trimmedEmail))
        {
            if (trimmedEmail.Length > EmailMaxLength || !EmailRegex().IsMatch(trimmedEmail))
                errors.Add(new FieldError("Email", "Email is not a valid email address."));
            else
                email = trimmedEmail;
        }

        decimal credit = 0;
        if (rawCredit is null)
        {
            errors.Add(new FieldError("CreditLimit", "CreditLimit is required."));
        }
        else if (rawCredit < 0 || rawCredit > CreditLimitMax)
        {
            errors.Add(new FieldError("CreditLimit", "CreditLimit must be between 0 and 1000000."));
        }
        else if (decimal.Round(rawCredit.Value, 2) != rawCredit.Value)
        {
            errors.Add(new FieldError("CreditLimit", "CreditLimit must have at most two decimal places."));
        }
        else
        {
            credit = decimal.Round(rawCredit.Value, 2);
        }

        if (errors.Count > before) return null;
        return new PartnerData(name!, taxNumber, type, email, credit);
    }
}
