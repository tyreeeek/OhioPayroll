using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OhioPayroll.Data.Migrations
{
    /// <inheritdoc />
    public partial class CodeRabbitFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StateWithholdingId",
                table: "CompanyInfo",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Actor",
                table: "AuditLog",
                type: "TEXT",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxLiability_PeriodDates",
                table: "TaxLiabilities",
                sql: "[PeriodEnd] >= [PeriodStart]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxLiability_Quarter",
                table: "TaxLiabilities",
                sql: "[Quarter] >= 1 AND [Quarter] <= 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxLiability_PeriodDates",
                table: "TaxLiabilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxLiability_Quarter",
                table: "TaxLiabilities");

            migrationBuilder.DropColumn(
                name: "StateWithholdingId",
                table: "CompanyInfo");

            migrationBuilder.DropColumn(
                name: "Actor",
                table: "AuditLog");
        }
    }
}
