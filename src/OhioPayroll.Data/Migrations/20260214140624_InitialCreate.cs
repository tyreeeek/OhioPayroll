using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OhioPayroll.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyBankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BankName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EncryptedRoutingNumber = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedAccountNumber = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefaultForChecks = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefaultForAch = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyBankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Ein = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EncryptedSsn = table.Column<string>(type: "TEXT", nullable: false),
                    SsnLast4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    PayType = table.Column<int>(type: "INTEGER", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    AnnualSalary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FederalFilingStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    OhioFilingStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    FederalAllowances = table.Column<int>(type: "INTEGER", nullable: false),
                    OhioExemptions = table.Column<int>(type: "INTEGER", nullable: false),
                    SchoolDistrictCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    MunicipalityCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    HireDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TerminationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalGrossPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalNetPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalFederalTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalStateTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalLocalTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalSocialSecurity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalMedicare = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalEmployerSocialSecurity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalEmployerMedicare = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalEmployerFuta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalEmployerSuta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PayFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentTaxYear = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalTaxRate = table.Column<decimal>(type: "TEXT", precision: 10, scale: 6, nullable: false),
                    SchoolDistrictRate = table.Column<decimal>(type: "TEXT", precision: 10, scale: 6, nullable: false),
                    SchoolDistrictCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    SutaRate = table.Column<decimal>(type: "TEXT", precision: 10, scale: 6, nullable: false),
                    BackupDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    NextCheckNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckOffsetX = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    CheckOffsetY = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxLiabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaxType = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxYear = table.Column<int>(type: "INTEGER", nullable: false),
                    Quarter = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AmountOwed = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PaymentReference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxLiabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaxYear = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    BracketStart = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BracketEnd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", precision: 10, scale: 6, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DistrictCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeBankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    EncryptedRoutingNumber = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedAccountNumber = table.Column<string>(type: "TEXT", nullable: false),
                    AccountType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeBankAccounts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Paychecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PayrollRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegularHours = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    RegularPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OvertimePay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    GrossPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FederalWithholding = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OhioStateWithholding = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SchoolDistrictTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LocalMunicipalityTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SocialSecurityTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MedicareTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EmployerSocialSecurity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EmployerMedicare = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EmployerFuta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EmployerSuta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    NetPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdGrossPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdFederalWithholding = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdOhioStateWithholding = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdSchoolDistrictTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdLocalTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdSocialSecurity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdMedicare = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    YtdNetPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    AchTraceNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsVoid = table.Column<bool>(type: "INTEGER", nullable: false),
                    VoidDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OriginalPaycheckId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paychecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paychecks_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Paychecks_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckRegister",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CheckNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PaycheckId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClearedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckRegister", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckRegister_Paychecks_PaycheckId",
                        column: x => x.PaycheckId,
                        principalTable: "Paychecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_EntityType_EntityId",
                table: "AuditLog",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                table: "AuditLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_CheckRegister_CheckNumber",
                table: "CheckRegister",
                column: "CheckNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckRegister_PaycheckId",
                table: "CheckRegister",
                column: "PaycheckId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckRegister_Status",
                table: "CheckRegister",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankAccounts_EmployeeId",
                table: "EmployeeBankAccounts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_IsActive",
                table: "Employees",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LastName_FirstName",
                table: "Employees",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_CheckNumber",
                table: "Paychecks",
                column: "CheckNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_EmployeeId",
                table: "Paychecks",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_PayrollRunId",
                table: "Paychecks",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_PayDate",
                table: "PayrollRuns",
                column: "PayDate");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_Status",
                table: "PayrollRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaxLiabilities_TaxYear_Quarter_TaxType",
                table: "TaxLiabilities",
                columns: new[] { "TaxYear", "Quarter", "TaxType" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxTables_TaxYear_Type_FilingStatus",
                table: "TaxTables",
                columns: new[] { "TaxYear", "Type", "FilingStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "CheckRegister");

            migrationBuilder.DropTable(
                name: "CompanyBankAccounts");

            migrationBuilder.DropTable(
                name: "CompanyInfo");

            migrationBuilder.DropTable(
                name: "EmployeeBankAccounts");

            migrationBuilder.DropTable(
                name: "PayrollSettings");

            migrationBuilder.DropTable(
                name: "TaxLiabilities");

            migrationBuilder.DropTable(
                name: "TaxTables");

            migrationBuilder.DropTable(
                name: "Paychecks");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "PayrollRuns");
        }
    }
}
