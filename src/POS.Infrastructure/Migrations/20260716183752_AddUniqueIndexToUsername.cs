using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A unique index refuses to be created over existing duplicates, which would abort the
            // upgrade with a raw SQLite error and no way forward. CreateUserUseCase has always
            // rejected repeated names, so a duplicate here can only come from outside the app
            // (manual edits, merged databases) — rare, but fatal for the whole rollout when it happens.
            //
            // ponytail: rename the newcomers instead of deleting them. Every account keeps its
            // history and its login; the oldest of each group keeps the original name, so whoever
            // has been using it is unaffected. Ordering by (CreatedAt, Id) leaves exactly one
            // untouched row per group even when legacy CreatedAt values are stored in odd formats,
            // because Id alone already breaks every tie.
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "Username" = "Username" || '_' || "Id"
                WHERE EXISTS (
                    SELECT 1 FROM "Users" AS older
                    WHERE older."Username" = "Users"."Username"
                      AND (older."CreatedAt" < "Users"."CreatedAt"
                           OR (older."CreatedAt" = "Users"."CreatedAt" AND older."Id" < "Users"."Id"))
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");
        }
    }
}
