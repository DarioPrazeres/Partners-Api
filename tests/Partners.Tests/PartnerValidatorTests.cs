using Partners.Core.Contracts;
using Partners.Core.Domain;
using Partners.Core.Errors;
using Partners.Core.Services;

namespace Partners.Tests;

public class PartnerValidatorTests
{
    private static CreatePartnerRequest Valid() => new()
    {
        Name = "Ana Silva",
        TaxNumber = "123456789",
        Type = "CUSTOMER",
        Email = null,
        CreditLimit = 10m
    };

    [Theory]
    [InlineData("001.234.567", "001234567")]
    [InlineData("001 234 567", "001234567")]
    [InlineData("001-234-567", "001234567")]
    [InlineData(" 001 .- 234 567 ", "001234567")]
    public void TaxNumber_is_normalized_preserving_leading_zeros(string input, string expected)
    {
        var request = Valid();
        request.TaxNumber = input;

        Assert.Equal(expected, PartnerValidator.ValidateCreate(request).TaxNumber);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678A")]
    [InlineData("١٢٣٤٥٦٧٨٩")] // dígitos árabe-índicos: não são ASCII
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TaxNumber_invalid_is_rejected(string? input)
    {
        var request = Valid();
        request.TaxNumber = input;

        var ex = Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateCreate(request));
        Assert.Contains(ex.Errors, e => e.Field == "TaxNumber");
    }

    [Fact]
    public void Name_is_trimmed_and_length_checked_after_trim()
    {
        var request = Valid();
        request.Name = "  Ana Silva  ";
        Assert.Equal("Ana Silva", PartnerValidator.ValidateCreate(request).Name);

        foreach (var bad in new[] { " A ", "", "   ", null, new string('x', 101) })
        {
            request.Name = bad;
            Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateCreate(request));
        }

        request.Name = new string('x', 100);
        Assert.Equal(100, PartnerValidator.ValidateCreate(request).Name.Length);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  ana@example.com ", "ana@example.com")]
    public void Email_blank_becomes_null_and_valid_is_trimmed(string? input, string? expected)
    {
        var request = Valid();
        request.Email = input;

        Assert.Equal(expected, PartnerValidator.ValidateCreate(request).Email);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("a b@example.com")]
    [InlineData("@example.com")]
    [InlineData("a@@example.com")]
    public void Email_invalid_is_rejected(string input)
    {
        var request = Valid();
        request.Email = input;

        var ex = Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateCreate(request));
        Assert.Contains(ex.Errors, e => e.Field == "Email");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1000000")]
    [InlineData("10.5")]
    [InlineData("10.99")]
    [InlineData("10.990")] // mesma quantia com escala extra: não é precisão excessiva
    public void CreditLimit_valid_values(string input)
    {
        var request = Valid();
        request.CreditLimit = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(request.CreditLimit, PartnerValidator.ValidateCreate(request).CreditLimit);
    }

    [Theory]
    [InlineData("10.999")]
    [InlineData("0.001")]
    [InlineData("-0.01")]
    [InlineData("1000000.01")]
    public void CreditLimit_invalid_values_are_rejected_not_rounded(string input)
    {
        var request = Valid();
        request.CreditLimit = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        var ex = Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateCreate(request));
        Assert.Contains(ex.Errors, e => e.Field == "CreditLimit");
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("supplier")]
    [InlineData(" BOTH ")]
    public void Type_valid(string input)
    {
        var request = Valid();
        request.Type = input;
        PartnerValidator.ValidateCreate(request);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void Type_invalid(string? input)
    {
        var request = Valid();
        request.Type = input;
        Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateCreate(request));
    }

    [Theory]
    [InlineData("AAAAAAAAAAA=", true)]
    [InlineData("AAAAAAAAB9E=", true)]
    [InlineData("not-base64!", false)]
    [InlineData("AAAA", false)]          // 3 bytes
    [InlineData("AAAAAAAAAAAAAAAA", false)] // 12 bytes
    [InlineData("", false)]
    public void Version_parsing(string input, bool ok)
    {
        Assert.Equal(ok, PartnerValidator.TryParseVersion(input, out var bytes));
        if (ok) Assert.Equal(8, bytes.Length);
    }

    [Fact]
    public void Update_requires_Version_and_IsActive()
    {
        var request = new UpdatePartnerRequest
        {
            Name = "Ana", TaxNumber = "123456789", Type = "CUSTOMER", CreditLimit = 1m
        };

        var ex = Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateUpdate(request));
        Assert.Contains(ex.Errors, e => e.Field == "Version");
        Assert.Contains(ex.Errors, e => e.Field == "IsActive");
    }

    [Fact]
    public void List_defaults_and_bounds()
    {
        var c = PartnerValidator.ValidateList(new PartnerListRequest());
        Assert.Equal(1, c.Page);
        Assert.Equal(20, c.PageSize);
        Assert.False(c.IncludeInactive);
        Assert.Null(c.NameTerm);
        Assert.Null(c.TaxTerm);

        Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateList(new PartnerListRequest { Page = 0 }));
        Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateList(new PartnerListRequest { PageSize = 0 }));
        Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateList(new PartnerListRequest { PageSize = 101 }));
        Assert.Throws<RequestValidationException>(() => PartnerValidator.ValidateList(new PartnerListRequest { Type = "NOPE" }));
    }

    [Fact]
    public void Search_with_only_separators_never_enables_the_tax_branch()
    {
        var c = PartnerValidator.ValidateList(new PartnerListRequest { Search = " .-. " });

        Assert.Equal(".-.", c.NameTerm);
        Assert.Null(c.TaxTerm); // um termo vazio faria LIKE '%%' e corresponderia a todos os NIF
    }

    [Fact]
    public void Search_is_normalized_for_tax_number()
    {
        var c = PartnerValidator.ValidateList(new PartnerListRequest { Search = "001.234" });

        Assert.Equal("001.234", c.NameTerm);
        Assert.Equal("001234", c.TaxTerm);
    }

    [Fact]
    public void Type_codes_roundtrip()
    {
        foreach (var t in Enum.GetValues<PartnerType>())
            Assert.Equal(t, PartnerTypeCodes.FromCode(PartnerTypeCodes.ToCode(t)));
    }
}
