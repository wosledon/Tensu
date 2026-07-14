using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tensu.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    webhook_ids = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    webhook_notification_id = table.Column<int>(type: "INTEGER", nullable: false),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    attempt_count = table.Column<int>(type: "INTEGER", nullable: false),
                    max_attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    last_status_code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    last_error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_attempt_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_success = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_next_retry_at",
                table: "webhook_deliveries",
                column: "next_retry_at");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_webhook_notification_id_is_success",
                table: "webhook_deliveries",
                columns: new[] { "webhook_notification_id", "is_success" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");
        }
    }
}
