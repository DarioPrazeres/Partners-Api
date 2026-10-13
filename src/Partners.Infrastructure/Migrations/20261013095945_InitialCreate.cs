using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partners.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Partners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    TaxNumber = table.Column<string>(type: "char(9)", unicode: false, fixedLength: true, maxLength: 9, nullable: false),
                    Type = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partners", x => x.Id);
                    table.CheckConstraint("CK_Partners_CreditLimit_Range", "[CreditLimit] >= 0 AND [CreditLimit] <= 1000000");
                    table.CheckConstraint("CK_Partners_TaxNumber_Digits", "LEN([TaxNumber]) = 9 AND [TaxNumber] NOT LIKE '%[^0-9]%'");
                    table.CheckConstraint("CK_Partners_Type_Values", "[Type] IN ('CUSTOMER', 'SUPPLIER', 'BOTH')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Partners_Name_Id",
                table: "Partners",
                columns: new[] { "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Partners_TaxNumber",
                table: "Partners",
                column: "TaxNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Partners");
        }
    }
}
