using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OhioPayroll.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedOhioTaxBrackets2025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ohio 2025 tax brackets (per Ohio Revised Code)
            // Bracket 1: $0 - $26,050 at 0% (no tax)
            // Bracket 2: $26,051 - $100,000 at 2.75% with base $342.00
            // Bracket 3: Over $100,000 at 3.125% with base $2,394.32
            migrationBuilder.InsertData(
                table: "TaxTables",
                columns: new[] { "TaxYear", "Type", "FilingStatus", "BracketStart", "BracketEnd", "Rate", "BaseAmount" },
                values: new object[,]
                {
                    { 2025, 1, 0, 0m, 26050m, 0.0000m, 0m },
                    { 2025, 1, 0, 26050m, 100000m, 0.0275m, 342.00m },
                    { 2025, 1, 0, 100000m, 999999999m, 0.03125m, 2394.32m },

                    // Ohio 2026 tax brackets (simplified two-bracket system)
                    // Bracket 1: $0 - $26,050 at 0% (no tax)
                    // Bracket 2: Over $26,050 at 2.75% with base $332.00
                    { 2026, 1, 0, 0m, 26050m, 0.0000m, 0m },
                    { 2026, 1, 0, 26050m, 999999999m, 0.0275m, 332.00m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TaxTables",
                keyColumns: new[] { "TaxYear", "Type" },
                keyValues: new object[] { 2025, 1 });

            migrationBuilder.DeleteData(
                table: "TaxTables",
                keyColumns: new[] { "TaxYear", "Type" },
                keyValues: new object[] { 2026, 1 });
        }
    }
}
