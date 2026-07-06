using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations;

public partial class PersistSmtpSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SmtpHost",
            table: "BUSINESS_SETTINGS",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SmtpPort",
            table: "BUSINESS_SETTINGS",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SmtpUser",
            table: "BUSINESS_SETTINGS",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SmtpPassword",
            table: "BUSINESS_SETTINGS",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "SmtpUseSsl",
            table: "BUSINESS_SETTINGS",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "SmtpSenderAddress",
            table: "BUSINESS_SETTINGS",
            type: "TEXT",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SmtpHost", table: "BUSINESS_SETTINGS");
        migrationBuilder.DropColumn(name: "SmtpPort", table: "BUSINESS_SETTINGS");
        migrationBuilder.DropColumn(name: "SmtpUser", table: "BUSINESS_SETTINGS");
        migrationBuilder.DropColumn(name: "SmtpPassword", table: "BUSINESS_SETTINGS");
        migrationBuilder.DropColumn(name: "SmtpUseSsl", table: "BUSINESS_SETTINGS");
        migrationBuilder.DropColumn(name: "SmtpSenderAddress", table: "BUSINESS_SETTINGS");
    }
}
