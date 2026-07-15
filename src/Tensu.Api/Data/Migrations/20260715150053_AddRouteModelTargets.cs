using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tensu.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteModelTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_route_models_models_target_model_id",
                table: "route_models");

            migrationBuilder.DropIndex(
                name: "IX_route_models_target_model_id",
                table: "route_models");

            migrationBuilder.DropColumn(
                name: "target_model_id",
                table: "route_models");

            migrationBuilder.CreateTable(
                name: "route_model_targets",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    route_model_id = table.Column<int>(type: "INTEGER", nullable: false),
                    model_id = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_model_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_route_model_targets_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_route_model_targets_route_models_route_model_id",
                        column: x => x.route_model_id,
                        principalTable: "route_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_route_model_targets_model_id",
                table: "route_model_targets",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_route_model_targets_route_model_id_model_id",
                table: "route_model_targets",
                columns: new[] { "route_model_id", "model_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "route_model_targets");

            migrationBuilder.AddColumn<int>(
                name: "target_model_id",
                table: "route_models",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_route_models_target_model_id",
                table: "route_models",
                column: "target_model_id");

            migrationBuilder.AddForeignKey(
                name: "FK_route_models_models_target_model_id",
                table: "route_models",
                column: "target_model_id",
                principalTable: "models",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
