using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tensu.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archived_request_logs",
                columns: table => new
                {
                    request_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    api_key_id = table.Column<int>(type: "INTEGER", nullable: true),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: true),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    model_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    resolved_model_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    provider_id = table.Column<int>(type: "INTEGER", nullable: true),
                    provider_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    input_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    input_tokens_after_compression = table.Column<int>(type: "INTEGER", nullable: true),
                    output_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    cache_hit = table.Column<bool>(type: "INTEGER", nullable: false),
                    semantic_cache_hit = table.Column<bool>(type: "INTEGER", nullable: false),
                    time_to_first_token_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    total_duration_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    output_tokens_per_second = table.Column<double>(type: "REAL", precision: 10, scale: 2, nullable: true),
                    input_cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    output_cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    compression_applied = table.Column<bool>(type: "INTEGER", nullable: false),
                    compression_strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    compression_mapping_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    retry_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_stream = table.Column<bool>(type: "INTEGER", nullable: false),
                    request_content = table.Column<string>(type: "TEXT", nullable: true),
                    response_content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archived_request_logs", x => x.request_id);
                });

            migrationBuilder.CreateTable(
                name: "compression_mappings",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    decompression_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    original_body = table.Column<string>(type: "TEXT", nullable: true),
                    compressed_body = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compression_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oauth_providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    protocol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    client_id = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    client_secret = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    authorization_endpoint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    token_endpoint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    userinfo_endpoint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    issuer = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    scope = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    parent_id = table.Column<int>(type: "INTEGER", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    enable_content_logging = table.Column<bool>(type: "INTEGER", nullable: false),
                    data_retention_days = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_organizations_organizations_parent_id",
                        column: x => x.parent_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    protocol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    base_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    health_status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    last_health_check_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    key_load_balance_strategy = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rate_limit_counters",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    scope = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    window_start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    window_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    value = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_limit_counters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "request_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    request_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    api_key_id = table.Column<int>(type: "INTEGER", nullable: true),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: true),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    model_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    resolved_model_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    provider_id = table.Column<int>(type: "INTEGER", nullable: true),
                    provider_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    input_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    input_tokens_after_compression = table.Column<int>(type: "INTEGER", nullable: true),
                    output_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    cache_hit = table.Column<bool>(type: "INTEGER", nullable: false),
                    semantic_cache_hit = table.Column<bool>(type: "INTEGER", nullable: false),
                    time_to_first_token_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    total_duration_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    output_tokens_per_second = table.Column<double>(type: "REAL", precision: 10, scale: 2, nullable: true),
                    input_cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    output_cost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: true),
                    currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    compression_applied = table.Column<bool>(type: "INTEGER", nullable: false),
                    compression_strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    compression_mapping_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    retry_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_stream = table.Column<bool>(type: "INTEGER", nullable: false),
                    request_content = table.Column<string>(type: "TEXT", nullable: true),
                    response_content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "semantic_cache_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    embedding = table.Column<string>(type: "TEXT", nullable: false),
                    request_body = table.Column<string>(type: "TEXT", nullable: false),
                    response_body = table.Column<string>(type: "TEXT", nullable: false),
                    is_stream = table.Column<bool>(type: "INTEGER", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semantic_cache_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: false),
                    username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    auth_provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    external_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    picture_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "models",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    provider_id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    supports_vision = table.Column<bool>(type: "INTEGER", nullable: false),
                    supports_reasoning = table.Column<bool>(type: "INTEGER", nullable: false),
                    supports_tool_use = table.Column<bool>(type: "INTEGER", nullable: false),
                    supports_thinking = table.Column<bool>(type: "INTEGER", nullable: false),
                    thinking_strengths = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    input_context_size = table.Column<int>(type: "INTEGER", nullable: false),
                    output_context_size = table.Column<int>(type: "INTEGER", nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    compression_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_models_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    provider_id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    key_value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    weight = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    rate_limit_rpm = table.Column<int>(type: "INTEGER", nullable: true),
                    rate_limit_tpm = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_health_check_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_keys_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    key_value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    key_prefix = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    allowed_models = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    ip_whitelist = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    rate_limit_rpm = table.Column<int>(type: "INTEGER", nullable: true),
                    rate_limit_tpm = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_keys_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_api_keys_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "model_capabilities",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    model_id = table.Column<int>(type: "INTEGER", nullable: false),
                    dimension = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    score = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    evidence = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_capabilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_model_capabilities_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "model_pricings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    model_id = table.Column<int>(type: "INTEGER", nullable: false),
                    input_price_per_million = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    output_price_per_million = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    cached_input_price_per_million = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    thinking_price_per_million = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    effective_from = table.Column<DateTime>(type: "TEXT", nullable: false),
                    effective_to = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_pricings", x => x.id);
                    table.ForeignKey(
                        name: "FK_model_pricings_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "route_models",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    target_model_id = table.Column<int>(type: "INTEGER", nullable: true),
                    fallback_model_id = table.Column<int>(type: "INTEGER", nullable: true),
                    routing_model_id = table.Column<int>(type: "INTEGER", nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_route_models_models_fallback_model_id",
                        column: x => x.fallback_model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_route_models_models_routing_model_id",
                        column: x => x.routing_model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_route_models_models_target_model_id",
                        column: x => x.target_model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "quotas",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    organization_id = table.Column<int>(type: "INTEGER", nullable: false),
                    api_key_id = table.Column<int>(type: "INTEGER", nullable: true),
                    rpm = table.Column<int>(type: "INTEGER", nullable: true),
                    tpm = table.Column<int>(type: "INTEGER", nullable: true),
                    concurrent_request_limit = table.Column<int>(type: "INTEGER", nullable: true),
                    daily_token_limit = table.Column<long>(type: "INTEGER", nullable: true),
                    monthly_token_limit = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotas", x => x.id);
                    table.ForeignKey(
                        name: "FK_quotas_api_keys_api_key_id",
                        column: x => x.api_key_id,
                        principalTable: "api_keys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_quotas_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "route_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    route_model_id = table.Column<int>(type: "INTEGER", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    condition = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    target_model_id = table.Column<int>(type: "INTEGER", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_route_rules_models_target_model_id",
                        column: x => x.target_model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_route_rules_route_models_route_model_id",
                        column: x => x.route_model_id,
                        principalTable: "route_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_key_prefix",
                table: "api_keys",
                column: "key_prefix");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_organization_id",
                table: "api_keys",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_user_id",
                table: "api_keys",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_archived_request_logs_model_name",
                table: "archived_request_logs",
                column: "model_name");

            migrationBuilder.CreateIndex(
                name: "IX_archived_request_logs_organization_id",
                table: "archived_request_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_archived_request_logs_request_id",
                table: "archived_request_logs",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_archived_request_logs_timestamp",
                table: "archived_request_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_compression_mappings_decompression_key",
                table: "compression_mappings",
                column: "decompression_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_capabilities_model_id_dimension",
                table: "model_capabilities",
                columns: new[] { "model_id", "dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_pricings_model_id",
                table: "model_pricings",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_models_provider_id_name",
                table: "models",
                columns: new[] { "provider_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oauth_providers_name",
                table: "oauth_providers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_name",
                table: "organizations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_parent_id",
                table: "organizations",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_path",
                table: "organizations",
                column: "path");

            migrationBuilder.CreateIndex(
                name: "IX_provider_keys_provider_id",
                table: "provider_keys",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_providers_name",
                table: "providers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotas_api_key_id",
                table: "quotas",
                column: "api_key_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotas_organization_id",
                table: "quotas",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_rate_limit_counters_scope_window_start",
                table: "rate_limit_counters",
                columns: new[] { "scope", "window_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rate_limit_counters_scope_window_start_window_seconds",
                table: "rate_limit_counters",
                columns: new[] { "scope", "window_start", "window_seconds" });

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_model_name",
                table: "request_logs",
                column: "model_name");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_organization_id",
                table: "request_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_request_id",
                table: "request_logs",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_request_logs_timestamp",
                table: "request_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_route_models_fallback_model_id",
                table: "route_models",
                column: "fallback_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_route_models_name",
                table: "route_models",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_route_models_routing_model_id",
                table: "route_models",
                column: "routing_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_route_models_target_model_id",
                table: "route_models",
                column: "target_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_route_rules_route_model_id",
                table: "route_rules",
                column: "route_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_route_rules_target_model_id",
                table: "route_rules",
                column: "target_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_semantic_cache_entries_expires_at",
                table: "semantic_cache_entries",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_semantic_cache_entries_model",
                table: "semantic_cache_entries",
                column: "model");

            migrationBuilder.CreateIndex(
                name: "IX_settings_key",
                table: "settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_users_organization_id",
                table: "users",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archived_request_logs");

            migrationBuilder.DropTable(
                name: "compression_mappings");

            migrationBuilder.DropTable(
                name: "model_capabilities");

            migrationBuilder.DropTable(
                name: "model_pricings");

            migrationBuilder.DropTable(
                name: "oauth_providers");

            migrationBuilder.DropTable(
                name: "provider_keys");

            migrationBuilder.DropTable(
                name: "quotas");

            migrationBuilder.DropTable(
                name: "rate_limit_counters");

            migrationBuilder.DropTable(
                name: "request_logs");

            migrationBuilder.DropTable(
                name: "route_rules");

            migrationBuilder.DropTable(
                name: "semantic_cache_entries");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "route_models");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "models");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "providers");
        }
    }
}
