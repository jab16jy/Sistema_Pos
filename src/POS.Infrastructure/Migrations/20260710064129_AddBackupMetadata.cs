using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Infrastructure.Persistence;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260710064129_AddBackupMetadata")]
    public partial class AddBackupMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastBackupAtUtc",
                table: "BUSINESS_SETTINGS",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastBackupLocation",
                table: "BUSINESS_SETTINGS",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastBackupSizeBytes",
                table: "BUSINESS_SETTINGS",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastBackupUsername",
                table: "BUSINESS_SETTINGS",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastBackupAtUtc",
                table: "BUSINESS_SETTINGS");

            migrationBuilder.DropColumn(
                name: "LastBackupLocation",
                table: "BUSINESS_SETTINGS");

            migrationBuilder.DropColumn(
                name: "LastBackupSizeBytes",
                table: "BUSINESS_SETTINGS");

            migrationBuilder.DropColumn(
                name: "LastBackupUsername",
                table: "BUSINESS_SETTINGS");
        }
    }
}
