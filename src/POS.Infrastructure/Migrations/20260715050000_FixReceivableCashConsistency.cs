using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using POS.Infrastructure.Persistence;

#nullable disable

namespace POS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260715050000_FixReceivableCashConsistency")]
public partial class FixReceivableCashConsistency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_cancelled",
            table: "RECEIVABLES",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "version",
            table: "RECEIVABLES",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "method",
            table: "RECEIVABLE_SETTLEMENTS",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "bank_or_platform",
            table: "RECEIVABLE_SETTLEMENTS",
            type: "TEXT",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "receivable_settlement_cash",
            table: "CASHIER_SHIFTS",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "receivable_settlement_digital",
            table: "CASHIER_SHIFTS",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "receivable_settlement_cash",
            table: "CASH_CLOSINGS",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "is_cancelled", table: "RECEIVABLES");
        migrationBuilder.DropColumn(name: "version", table: "RECEIVABLES");
        migrationBuilder.DropColumn(name: "method", table: "RECEIVABLE_SETTLEMENTS");
        migrationBuilder.DropColumn(name: "bank_or_platform", table: "RECEIVABLE_SETTLEMENTS");
        migrationBuilder.DropColumn(name: "receivable_settlement_cash", table: "CASHIER_SHIFTS");
        migrationBuilder.DropColumn(name: "receivable_settlement_digital", table: "CASHIER_SHIFTS");
        migrationBuilder.DropColumn(name: "receivable_settlement_cash", table: "CASH_CLOSINGS");
    }
}
