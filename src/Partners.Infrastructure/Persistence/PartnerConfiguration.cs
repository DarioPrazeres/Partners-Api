using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Partners.Core.Domain;

namespace Partners.Infrastructure.Persistence;

public sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public const string TaxNumberIndexName = "IX_Partners_TaxNumber";

    // Colação explícita: a pesquisa e a ordenação por nome não dependem da colação da base de dados.
    public const string NameCollation = "Latin1_General_100_CI_AS";

    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        var typeConverter = new ValueConverter<PartnerType, string>(
            v => PartnerTypeCodes.ToCode(v),
            v => PartnerTypeCodes.FromCode(v));

        // O SQL Server devolve datetime2 com Kind=Unspecified; garantimos UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.ToTable("Partners", t =>
        {
            t.HasCheckConstraint("CK_Partners_TaxNumber_Digits",
                "LEN([TaxNumber]) = 9 AND [TaxNumber] NOT LIKE '%[^0-9]%'");
            t.HasCheckConstraint("CK_Partners_CreditLimit_Range",
                "[CreditLimit] >= 0 AND [CreditLimit] <= 1000000");
            t.HasCheckConstraint("CK_Partners_Type_Values",
                "[Type] IN ('CUSTOMER', 'SUPPLIER', 'BOTH')");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100)
            .UseCollation(NameCollation);

        builder.Property(p => p.TaxNumber)
            .IsRequired()
            .HasMaxLength(9)
            .IsUnicode(false)
            .IsFixedLength();

        builder.Property(p => p.Type)
            .IsRequired()
            .HasMaxLength(8)
            .IsUnicode(false)
            .HasConversion(typeConverter);

        builder.Property(p => p.Email).HasMaxLength(254);

        builder.Property(p => p.CreditLimit).HasPrecision(18, 2);

        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.Property(p => p.CreatedAt).HasColumnType("datetime2").HasConversion(utcConverter);
        builder.Property(p => p.UpdatedAt).HasColumnType("datetime2").HasConversion(utcConverter);

        // Garantia de unicidade do NIF na base de dados (inclui entidades inativas).
        builder.HasIndex(p => p.TaxNumber).IsUnique().HasDatabaseName(TaxNumberIndexName);

        // Suporta a ordenação Name, Id usada na listagem.
        builder.HasIndex(p => new { p.Name, p.Id }).HasDatabaseName("IX_Partners_Name_Id");
    }
}
