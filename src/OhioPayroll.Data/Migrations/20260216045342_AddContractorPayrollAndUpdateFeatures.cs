using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OhioPayroll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractorPayrollAndUpdateFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoCheckForUpdates",
                table: "PayrollSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractorPayrollLockDate",
                table: "PayrollSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastKnownVersion",
                table: "PayrollSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdateCheck",
                table: "PayrollSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdateChannel",
                table: "PayrollSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UpdateChannelUrl",
                table: "PayrollSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyRate",
                table: "Contractors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Contractors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateType",
                table: "Contractors",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContractorNameSnapshot",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractorPayrollRunId",
                table: "ContractorPayments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DaysWorked",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCheck",
                table: "ContractorPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPaystub",
                table: "ContractorPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursWorked",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "ContractorPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "ContractorPayments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RateAtPayment",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateTypeAtPayment",
                table: "ContractorPayments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PaycheckId",
                table: "CheckRegister",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "ContractorPaymentId",
                table: "CheckRegister",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractorPayrollRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinalizedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorPayrollRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UpdateHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromVersion = table.Column<string>(type: "TEXT", nullable: false),
                    ToVersion = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    WasSuccessful = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_ContractorPayrollRunId",
                table: "ContractorPayments",
                column: "ContractorPayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckRegister_ContractorPaymentId",
                table: "CheckRegister",
                column: "ContractorPaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckRegister_ContractorPayments_ContractorPaymentId",
                table: "CheckRegister",
                column: "ContractorPaymentId",
                principalTable: "ContractorPayments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractorPayments_ContractorPayrollRuns_ContractorPayrollRunId",
                table: "ContractorPayments",
                column: "ContractorPayrollRunId",
                principalTable: "ContractorPayrollRuns",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckRegister_ContractorPayments_ContractorPaymentId",
                table: "CheckRegister");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractorPayments_ContractorPayrollRuns_ContractorPayrollRunId",
                table: "ContractorPayments");

            migrationBuilder.DropTable(
                name: "ContractorPayrollRuns");

            migrationBuilder.DropTable(
                name: "UpdateHistory");

            migrationBuilder.DropIndex(
                name: "IX_ContractorPayments_ContractorPayrollRunId",
                table: "ContractorPayments");

            migrationBuilder.DropIndex(
                name: "IX_CheckRegister_ContractorPaymentId",
                table: "CheckRegister");

            migrationBuilder.DropColumn(
                name: "AutoCheckForUpdates",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ContractorPayrollLockDate",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "LastKnownVersion",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "LastUpdateCheck",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "UpdateChannel",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "UpdateChannelUrl",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "DailyRate",
                table: "Contractors");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Contractors");

            migrationBuilder.DropColumn(
                name: "RateType",
                table: "Contractors");

            migrationBuilder.DropColumn(
                name: "ContractorNameSnapshot",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "ContractorPayrollRunId",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "DaysWorked",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "HasCheck",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "HasPaystub",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "HoursWorked",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "RateAtPayment",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "RateTypeAtPayment",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "ContractorPaymentId",
                table: "CheckRegister");

            migrationBuilder.AlterColumn<int>(
                name: "PaycheckId",
                table: "CheckRegister",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
