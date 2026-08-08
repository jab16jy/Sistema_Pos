using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DefaultDeferredBalancePercentToZero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "MaximumDeferredBalancePercent",
                table: "BUSINESS_SETTINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldDefaultValue: 50m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "MaximumDeferredBalancePercent",
                table: "BUSINESS_SETTINGS",
                type: "TEXT",
                nullable: false,
                defaultValue: 50m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldDefaultValue: 0m);
        }
    }
}
