using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OhioPayroll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contractors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BusinessName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EncryptedTin = table.Column<string>(type: "TEXT", nullable: false),
                    TinLast4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    IsEin = table.Column<bool>(type: "INTEGER", nullable: false),
                    BusinessType = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Is1099Exempt = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContractorPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractorId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TaxYear = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorPayments_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_ContractorId_TaxYear",
                table: "ContractorPayments",
                columns: new[] { "ContractorId", "TaxYear" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_PaymentDate",
                table: "ContractorPayments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_IsActive",
                table: "Contractors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_Name",
                table: "Contractors",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractorPayments");

            migrationBuilder.DropTable(
                name: "Contractors");
        }
    }
}
