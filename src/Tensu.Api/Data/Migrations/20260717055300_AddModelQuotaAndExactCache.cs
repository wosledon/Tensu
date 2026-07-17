using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tensu.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModelQuotaAndExactCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "model_id",
                table: "quotas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exact_cache_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    cache_key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    response_body = table.Column<string>(type: "TEXT", nullable: false),
                    is_stream = table.Column<bool>(type: "INTEGER", nullable: false),
                    hit_count = table.Column<int>(type: "INTEGER", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exact_cache_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotas_model_id",
                table: "quotas",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_exact_cache_entries_cache_key",
                table: "exact_cache_entries",
                column: "cache_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exact_cache_entries_expires_at",
                table: "exact_cache_entries",
                column: "expires_at");

            migrationBuilder.AddForeignKey(
                name: "FK_quotas_models_model_id",
                table: "quotas",
                column: "model_id",
                principalTable: "models",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quotas_models_model_id",
                table: "quotas");

            migrationBuilder.DropTable(
                name: "exact_cache_entries");

            migrationBuilder.DropIndex(
                name: "IX_quotas_model_id",
                table: "quotas");

            migrationBuilder.DropColumn(
                name: "model_id",
                table: "quotas");
        }
    }
}
