using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using POS.Infrastructure.Persistence;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260710065502_EnforceProductBarcodeUniqueness")]
    public partial class EnforceProductBarcodeUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize blank/whitespace-only barcodes to NULL so they don't collide
            // with the unique index. SQLite TRIM only strips spaces, so we explicitly
            // strip tabs, line feeds, and carriage returns as well.
            migrationBuilder.Sql("""
                UPDATE "PRODUCTS"
                SET "Barcode" = NULL
                WHERE "Barcode" IS NOT NULL
                  AND length(
                    TRIM(
                      REPLACE(
                        REPLACE(
                          REPLACE("Barcode",
                            char(9),  ''),
                          char(10), ''),
                        char(13), '')
                    )
                  ) = 0;
                """);

            // Log every duplicate remapping before rewriting it so operators have an
            // audit trail and can print replacement labels.
            migrationBuilder.Sql("""
                INSERT INTO "AUDIT_LOGS" ("Id", "UserId", "Username", "Action", "EntityName", "EntityId", "Details", "CreatedAt")
                SELECT
                    lower(hex(randomblob(16))),
                    NULL,
                    'Sistema',
                    'BarcodeMigration',
                    'Product',
                    "Id",
                    'Duplicate barcode cleared: ' || "Barcode" || ' -> INT-' || "Id",
                    datetime('now')
                FROM "PRODUCTS"
                WHERE "Barcode" IS NOT NULL
                  AND rowid NOT IN (
                      SELECT MIN(rowid)
                      FROM "PRODUCTS"
                      WHERE "Barcode" IS NOT NULL
                      GROUP BY "Barcode"
                  );
                """);

            // Remap duplicate barcodes to deterministic persistent internal codes
            // so products remain scannable instead of silently losing their labels.
            migrationBuilder.Sql("""
                UPDATE "PRODUCTS"
                SET "Barcode" = 'INT-' || "Id"
                WHERE "Barcode" IS NOT NULL
                  AND rowid NOT IN (
                      SELECT MIN(rowid)
                      FROM "PRODUCTS"
                      WHERE "Barcode" IS NOT NULL
                      GROUP BY "Barcode"
                  );
                """);

            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_Barcode",
                table: "PRODUCTS",
                column: "Barcode");

        }
    }
}
