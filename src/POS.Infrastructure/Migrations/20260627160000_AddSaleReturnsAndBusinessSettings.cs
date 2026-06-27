using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleReturnsAndBusinessSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BUSINESS_SETTINGS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BusinessAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrinterCommand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrinterWorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BackupDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BUSINESS_SETTINGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SALE_RETURNS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalSaleId = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SALE_RETURNS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURNS_OriginalSaleId",
                table: "SALE_RETURNS",
                column: "OriginalSaleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BUSINESS_SETTINGS");

            migrationBuilder.DropTable(
                name: "SALE_RETURNS");
        }
    }
}
