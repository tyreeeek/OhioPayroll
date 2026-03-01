using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OhioPayroll.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PayrollRuns",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContractorPayrollRuns",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CompanyBankAccounts",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayrollRuns_PayDate",
                table: "ContractorPayrollRuns",
                column: "PayDate");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayrollRuns_Status",
                table: "ContractorPayrollRuns",
                column: "Status");

            // UNIQUE constraint: Only one default bank account per type
            migrationBuilder.CreateIndex(
                name: "IX_CompanyBankAccounts_IsDefaultForChecks_Unique",
                table: "CompanyBankAccounts",
                column: "IsDefaultForChecks",
                unique: true,
                filter: "IsDefaultForChecks = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyBankAccounts_IsDefaultForAch_Unique",
                table: "CompanyBankAccounts",
                column: "IsDefaultForAch",
                unique: true,
                filter: "IsDefaultForAch = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankAccounts_EmployeeId_IsActive_Unique",
                table: "EmployeeBankAccounts",
                columns: new[] { "EmployeeId", "IsActive" },
                unique: true,
                filter: "IsActive = 1");

            // CHECK constraints for financial data - must use raw SQL for SQLite
            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS check_paychecks_grosspay_positive
                BEFORE INSERT ON Paychecks
                FOR EACH ROW
                WHEN NEW.GrossPay < 0
                BEGIN
                    SELECT RAISE(ABORT, 'GrossPay must be non-negative');
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS check_paychecks_netpay_positive
                BEFORE INSERT ON Paychecks
                FOR EACH ROW
                WHEN NEW.NetPay < 0
                BEGIN
                    SELECT RAISE(ABORT, 'NetPay must be non-negative');
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS check_contractorpayments_amount_positive
                BEFORE INSERT ON ContractorPayments
                FOR EACH ROW
                WHEN NEW.Amount < 0
                BEGIN
                    SELECT RAISE(ABORT, 'ContractorPayment Amount must be non-negative');
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS check_paychecks_grosspay_positive_update
                BEFORE UPDATE ON Paychecks
                FOR EACH ROW
                WHEN NEW.GrossPay < 0
                BEGIN
                    SELECT RAISE(ABORT, 'GrossPay must be non-negative');
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS check_paychecks_netpay_positive_update
                BEFORE UPDATE ON Paychecks
                FOR EACH ROW
                WHEN NEW.NetPay < 0
                BEGIN
                    SELECT RAISE(ABORT, 'NetPay must be non-negative');
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS check_contractorpayments_amount_positive_update
                BEFORE UPDATE ON ContractorPayments
                FOR EACH ROW
                WHEN NEW.Amount < 0
                BEGIN
                    SELECT RAISE(ABORT, 'ContractorPayment Amount must be non-negative');
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop triggers
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS check_paychecks_grosspay_positive;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS check_paychecks_netpay_positive;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS check_contractorpayments_amount_positive;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS check_paychecks_grosspay_positive_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS check_paychecks_netpay_positive_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS check_contractorpayments_amount_positive_update;");

            // Drop unique indexes
            migrationBuilder.DropIndex(
                name: "IX_EmployeeBankAccounts_EmployeeId_IsActive_Unique",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_CompanyBankAccounts_IsDefaultForAch_Unique",
                table: "CompanyBankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_CompanyBankAccounts_IsDefaultForChecks_Unique",
                table: "CompanyBankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ContractorPayrollRuns_PayDate",
                table: "ContractorPayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_ContractorPayrollRuns_Status",
                table: "ContractorPayrollRuns");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContractorPayrollRuns");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CompanyBankAccounts");
        }
    }
}
