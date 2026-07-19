using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tensu.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageDetailTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cached_input_tokens",
                table: "request_logs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reasoning_tokens",
                table: "request_logs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cached_input_tokens",
                table: "archived_request_logs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reasoning_tokens",
                table: "archived_request_logs",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cached_input_tokens",
                table: "request_logs");

            migrationBuilder.DropColumn(
                name: "reasoning_tokens",
                table: "request_logs");

            migrationBuilder.DropColumn(
                name: "cached_input_tokens",
                table: "archived_request_logs");

            migrationBuilder.DropColumn(
                name: "reasoning_tokens",
                table: "archived_request_logs");
        }
    }
}
