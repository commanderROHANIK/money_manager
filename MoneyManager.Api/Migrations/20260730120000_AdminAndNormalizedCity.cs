using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdminAndNormalizedCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCity",
                table: "RentalProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalProperties_NormalizedCity_PropertyType_CurrencyCode",
                table: "RentalProperties",
                columns: new[] { "NormalizedCity", "PropertyType", "CurrencyCode" });

            // An existing instance has no administrator, and exchange rates are now
            // admin-only to write. The earliest account is the one that set the instance up,
            // which matches how a fresh instance assigns it at registration.
            migrationBuilder.Sql(
                "UPDATE Users SET IsAdmin = 1 WHERE Id = (SELECT MIN(Id) FROM Users);");

            // NormalizedCity is deliberately left null here rather than backfilled with
            // SQL UPPER(): that function is ASCII-only in SQLite, so it would write "GYőR"
            // where the application writes "GYŐR" and the two would never match — the exact
            // defect this column exists to fix. The backfill runs at startup instead, in
            // C#, where ToUpperInvariant is correct for every alphabet.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalProperties_NormalizedCity_PropertyType_CurrencyCode",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "NormalizedCity",
                table: "RentalProperties");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }
    }
}
