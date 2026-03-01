using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OhioPayroll.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionReadinessFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractorPayments_Contractors_ContractorId",
                table: "ContractorPayments");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ContractorPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_TinLast4",
                table: "Contractors",
                column: "TinLast4");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_ContractorId_IsDeleted",
                table: "ContractorPayments",
                columns: new[] { "ContractorId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContractorPayments_Contractors_ContractorId",
                table: "ContractorPayments",
                column: "ContractorId",
                principalTable: "Contractors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractorPayments_Contractors_ContractorId",
                table: "ContractorPayments");

            migrationBuilder.DropIndex(
                name: "IX_Contractors_TinLast4",
                table: "Contractors");

            migrationBuilder.DropIndex(
                name: "IX_ContractorPayments_ContractorId_IsDeleted",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ContractorPayments");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractorPayments_Contractors_ContractorId",
                table: "ContractorPayments",
                column: "ContractorId",
                principalTable: "Contractors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
