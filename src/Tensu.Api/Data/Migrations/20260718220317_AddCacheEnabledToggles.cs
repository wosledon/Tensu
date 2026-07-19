using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tensu.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheEnabledToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cache_enabled",
                table: "organizations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "cache_enabled",
                table: "models",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cache_enabled",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "cache_enabled",
                table: "models");
        }
    }
}
