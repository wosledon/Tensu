using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tensu.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyStatsAdminAuditWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "compression_enabled",
                table: "organizations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "admin_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    details = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    ip_address = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_stats",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: true),
                    model_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    provider_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    total_requests = table.Column<int>(type: "INTEGER", nullable: false),
                    success_requests = table.Column<int>(type: "INTEGER", nullable: false),
                    failed_requests = table.Column<int>(type: "INTEGER", nullable: false),
                    rate_limited_requests = table.Column<int>(type: "INTEGER", nullable: false),
                    cache_hits = table.Column<int>(type: "INTEGER", nullable: false),
                    total_input_tokens = table.Column<long>(type: "INTEGER", nullable: false),
                    total_output_tokens = table.Column<long>(type: "INTEGER", nullable: false),
                    total_input_tokens_after_compression = table.Column<long>(type: "INTEGER", nullable: false),
                    total_input_cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    total_output_cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    avg_latency_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    p50_latency_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    p95_latency_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    p99_latency_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    avg_ttft_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    avg_output_tokens_per_second = table.Column<double>(type: "REAL", precision: 10, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_stats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_deletion_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: true),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    api_key_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_request_logs = table.Column<int>(type: "INTEGER", nullable: false),
                    deleted_archived_logs = table.Column<int>(type: "INTEGER", nullable: false),
                    request_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_deletion_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    secret = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    events = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_notifications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_action",
                table: "admin_audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_timestamp",
                table: "admin_audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_user_id",
                table: "admin_audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_stats_date",
                table: "daily_stats",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "IX_daily_stats_date_organization_id_model_name_provider_name",
                table: "daily_stats",
                columns: new[] { "date", "organization_id", "model_name", "provider_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_deletion_requests_created_at",
                table: "data_deletion_requests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_deletion_requests_request_id",
                table: "data_deletion_requests",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_deletion_requests_status",
                table: "data_deletion_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_audit_logs");

            migrationBuilder.DropTable(
                name: "daily_stats");

            migrationBuilder.DropTable(
                name: "data_deletion_requests");

            migrationBuilder.DropTable(
                name: "webhook_notifications");

            migrationBuilder.DropColumn(
                name: "compression_enabled",
                table: "organizations");
        }
    }
}
