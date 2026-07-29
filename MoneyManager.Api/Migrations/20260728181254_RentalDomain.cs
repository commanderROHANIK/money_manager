using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class RentalDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RentAmount",
                table: "RentalProperties");

            migrationBuilder.RenameColumn(
                name: "RentDueDate",
                table: "RentalProperties",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "IsRented",
                table: "RentalProperties",
                newName: "Status");

            migrationBuilder.AddColumn<int>(
                name: "Bedrooms",
                table: "RentalProperties",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PropertyType",
                table: "RentalProperties",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaleDate",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SizeSqm",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoanType",
                table: "Loans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPayment",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RentalPropertyId",
                table: "Loans",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TermMonths",
                table: "Loans",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Leases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RentalPropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantName = table.Column<string>(type: "TEXT", nullable: false),
                    TenantEmail = table.Column<string>(type: "TEXT", nullable: true),
                    TenantPhone = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    RentDueDayOfMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leases_RentalProperties_RentalPropertyId",
                        column: x => x.RentalPropertyId,
                        principalTable: "RentalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Leases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RentalPropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyEvents_RentalProperties_RentalPropertyId",
                        column: x => x.RentalPropertyId,
                        principalTable: "RentalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RentalPropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyTransactions_RentalProperties_RentalPropertyId",
                        column: x => x.RentalPropertyId,
                        principalTable: "RentalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyValuations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RentalPropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    ValuedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyValuations_RentalProperties_RentalPropertyId",
                        column: x => x.RentalPropertyId,
                        principalTable: "RentalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyValuations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentPricePoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RentalPropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentPricePoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentPricePoints_RentalProperties_RentalPropertyId",
                        column: x => x.RentalPropertyId,
                        principalTable: "RentalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RentPricePoints_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RentalProperties_UserId_City",
                table: "RentalProperties",
                columns: new[] { "UserId", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_RentalPropertyId",
                table: "Loans",
                column: "RentalPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_RentalPropertyId_StartDate",
                table: "Leases",
                columns: new[] { "RentalPropertyId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_UserId",
                table: "Leases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyEvents_RentalPropertyId_OccurredOn",
                table: "PropertyEvents",
                columns: new[] { "RentalPropertyId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyEvents_UserId",
                table: "PropertyEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_RentalPropertyId_Date",
                table: "PropertyTransactions",
                columns: new[] { "RentalPropertyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTransactions_UserId",
                table: "PropertyTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValuations_RentalPropertyId_ValuedOn",
                table: "PropertyValuations",
                columns: new[] { "RentalPropertyId", "ValuedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValuations_UserId",
                table: "PropertyValuations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RentPricePoints_RentalPropertyId_Source_EffectiveFrom",
                table: "RentPricePoints",
                columns: new[] { "RentalPropertyId", "Source", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_RentPricePoints_UserId",
                table: "RentPricePoints",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_RentalProperties_RentalPropertyId",
                table: "Loans",
                column: "RentalPropertyId",
                principalTable: "RentalProperties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loans_RentalProperties_RentalPropertyId",
                table: "Loans");

            migrationBuilder.DropTable(
                name: "Leases");

            migrationBuilder.DropTable(
                name: "PropertyEvents");

            migrationBuilder.DropTable(
                name: "PropertyTransactions");

            migrationBuilder.DropTable(
                name: "PropertyValuations");

            migrationBuilder.DropTable(
                name: "RentPricePoints");

            migrationBuilder.DropIndex(
                name: "IX_RentalProperties_UserId_City",
                table: "RentalProperties");

            migrationBuilder.DropIndex(
                name: "IX_Loans_RentalPropertyId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "Bedrooms",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "City",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "PropertyType",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "SaleDate",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "SizeSqm",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "LoanType",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "MonthlyPayment",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "RentalPropertyId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "TermMonths",
                table: "Loans");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "RentalProperties",
                newName: "IsRented");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "RentalProperties",
                newName: "RentDueDate");

            migrationBuilder.AddColumn<decimal>(
                name: "RentAmount",
                table: "RentalProperties",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
