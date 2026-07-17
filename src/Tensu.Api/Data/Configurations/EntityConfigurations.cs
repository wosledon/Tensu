using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tensu.Core.Entities;

namespace Tensu.Api.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ParentId).HasColumnName("parent_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Path).HasColumnName("path").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.EnableContentLogging).HasColumnName("enable_content_logging");
        builder.Property(e => e.CompressionEnabled).HasColumnName("compression_enabled");
        builder.Property(e => e.DataRetentionDays).HasColumnName("data_retention_days");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.Path);
        builder.HasIndex(e => e.Name);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").HasMaxLength(200);
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.AuthProvider).HasColumnName("auth_provider").HasMaxLength(50);
        builder.Property(e => e.ExternalId).HasColumnName("external_id").HasMaxLength(200);
        builder.Property(e => e.PictureUrl).HasColumnName("picture_url").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("is_active");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.LastLoginAt).HasColumnName("last_login_at");

        builder.HasOne(e => e.Organization).WithMany(e => e.Users).HasForeignKey(e => e.OrganizationId);
        builder.HasIndex(e => e.Username).IsUnique();
        builder.HasIndex(e => e.Email);
    }
}

public class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("providers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Protocol).HasColumnName("protocol").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.BaseUrl).HasColumnName("base_url").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.HealthStatus).HasColumnName("health_status").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.LastHealthCheckAt).HasColumnName("last_health_check_at");
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.KeyLoadBalanceStrategy).HasColumnName("key_load_balance_strategy").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Name).IsUnique();
    }
}

public class ProviderKeyConfiguration : IEntityTypeConfiguration<ProviderKey>
{
    public void Configure(EntityTypeBuilder<ProviderKey> builder)
    {
        builder.ToTable("provider_keys");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ProviderId).HasColumnName("provider_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.KeyValue).HasColumnName("key_value").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Weight).HasColumnName("weight");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.RateLimitRpm).HasColumnName("rate_limit_rpm");
        builder.Property(e => e.RateLimitTpm).HasColumnName("rate_limit_tpm");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.LastHealthCheckAt).HasColumnName("last_health_check_at");

        builder.HasOne(e => e.Provider).WithMany(e => e.Keys).HasForeignKey(e => e.ProviderId);
    }
}

public class ModelConfiguration : IEntityTypeConfiguration<Model>
{
    public void Configure(EntityTypeBuilder<Model> builder)
    {
        builder.ToTable("models");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ProviderId).HasColumnName("provider_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.SupportsVision).HasColumnName("supports_vision");
        builder.Property(e => e.SupportsReasoning).HasColumnName("supports_reasoning");
        builder.Property(e => e.SupportsToolUse).HasColumnName("supports_tool_use");
        builder.Property(e => e.SupportsThinking).HasColumnName("supports_thinking");
        builder.Property(e => e.ThinkingStrengths).HasColumnName("thinking_strengths").HasMaxLength(500);
        builder.Property(e => e.InputContextSize).HasColumnName("input_context_size");
        builder.Property(e => e.OutputContextSize).HasColumnName("output_context_size");
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.CompressionEnabled).HasColumnName("compression_enabled");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Provider).WithMany(e => e.Models).HasForeignKey(e => e.ProviderId);
        builder.HasIndex(e => new { e.ProviderId, e.Name }).IsUnique();
    }
}

public class ModelPricingConfiguration : IEntityTypeConfiguration<ModelPricing>
{
    public void Configure(EntityTypeBuilder<ModelPricing> builder)
    {
        builder.ToTable("model_pricings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ModelId).HasColumnName("model_id");
        builder.Property(e => e.InputPricePerMillionTokens).HasColumnName("input_price_per_million").HasPrecision(18, 6);
        builder.Property(e => e.OutputPricePerMillionTokens).HasColumnName("output_price_per_million").HasPrecision(18, 6);
        builder.Property(e => e.CachedInputPricePerMillionTokens).HasColumnName("cached_input_price_per_million").HasPrecision(18, 6);
        builder.Property(e => e.ThinkingPricePerMillionTokens).HasColumnName("thinking_price_per_million").HasPrecision(18, 6);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(e => e.ExchangeRate).HasColumnName("exchange_rate").HasPrecision(18, 6);
        builder.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(e => e.EffectiveTo).HasColumnName("effective_to");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.Model).WithMany(e => e.Pricings).HasForeignKey(e => e.ModelId);
    }
}

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.KeyValue).HasColumnName("key_value").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(20);
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.AllowedModels).HasColumnName("allowed_models").HasMaxLength(5000);
        builder.Property(e => e.IpWhitelist).HasColumnName("ip_whitelist").HasMaxLength(2000);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.RateLimitRpm).HasColumnName("rate_limit_rpm");
        builder.Property(e => e.RateLimitTpm).HasColumnName("rate_limit_tpm");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Organization).WithMany(e => e.ApiKeys).HasForeignKey(e => e.OrganizationId);
        builder.HasOne(e => e.User).WithMany(e => e.ApiKeys).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => e.KeyPrefix);
    }
}

public class QuotaConfiguration : IEntityTypeConfiguration<Quota>
{
    public void Configure(EntityTypeBuilder<Quota> builder)
    {
        builder.ToTable("quotas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.ApiKeyId).HasColumnName("api_key_id");
        builder.Property(e => e.ModelId).HasColumnName("model_id");
        builder.Property(e => e.Rpm).HasColumnName("rpm");
        builder.Property(e => e.Tpm).HasColumnName("tpm");
        builder.Property(e => e.ConcurrentRequestLimit).HasColumnName("concurrent_request_limit");
        builder.Property(e => e.DailyTokenLimit).HasColumnName("daily_token_limit");
        builder.Property(e => e.MonthlyTokenLimit).HasColumnName("monthly_token_limit");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Organization).WithMany(e => e.Quotas).HasForeignKey(e => e.OrganizationId);
        builder.HasOne(e => e.ApiKey).WithMany().HasForeignKey(e => e.ApiKeyId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Model).WithMany().HasForeignKey(e => e.ModelId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RateLimitCounterConfiguration : IEntityTypeConfiguration<RateLimitCounter>
{
    public void Configure(EntityTypeBuilder<RateLimitCounter> builder)
    {
        builder.ToTable("rate_limit_counters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Scope).HasColumnName("scope").HasMaxLength(500).IsRequired();
        builder.Property(e => e.WindowStart).HasColumnName("window_start");
        builder.Property(e => e.WindowSeconds).HasColumnName("window_seconds");
        builder.Property(e => e.Value).HasColumnName("value");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.Scope, e.WindowStart }).IsUnique();
        builder.HasIndex(e => new { e.Scope, e.WindowStart, e.WindowSeconds });
    }
}

public class CompressionMappingConfiguration : IEntityTypeConfiguration<CompressionMapping>
{
    public void Configure(EntityTypeBuilder<CompressionMapping> builder)
    {
        builder.ToTable("compression_mappings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.DecompressionKey).HasColumnName("decompression_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Strategy).HasColumnName("strategy").HasMaxLength(100).IsRequired();
        builder.Property(e => e.OriginalBody).HasColumnName("original_body");
        builder.Property(e => e.CompressedBody).HasColumnName("compressed_body").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.DecompressionKey).IsUnique();
    }
}

public class SemanticCacheEntryConfiguration : IEntityTypeConfiguration<SemanticCacheEntry>
{
    public void Configure(EntityTypeBuilder<SemanticCacheEntry> builder)
    {
        builder.ToTable("semantic_cache_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Model).HasColumnName("model").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Embedding).HasColumnName("embedding").IsRequired();
        builder.Property(e => e.RequestBody).HasColumnName("request_body").IsRequired();
        builder.Property(e => e.ResponseBody).HasColumnName("response_body").IsRequired();
        builder.Property(e => e.IsStream).HasColumnName("is_stream");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.Model);
        builder.HasIndex(e => e.ExpiresAt);
    }
}

public class ExactCacheEntryConfiguration : IEntityTypeConfiguration<ExactCacheEntry>
{
    public void Configure(EntityTypeBuilder<ExactCacheEntry> builder)
    {
        builder.ToTable("exact_cache_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.CacheKey).HasColumnName("cache_key").HasMaxLength(500).IsRequired();
        builder.Property(e => e.ResponseBody).HasColumnName("response_body").IsRequired();
        builder.Property(e => e.IsStream).HasColumnName("is_stream");
        builder.Property(e => e.HitCount).HasColumnName("hit_count");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.CacheKey).IsUnique();
        builder.HasIndex(e => e.ExpiresAt);
    }
}

public class RouteModelConfiguration : IEntityTypeConfiguration<RouteModel>
{
    public void Configure(EntityTypeBuilder<RouteModel> builder)
    {
        builder.ToTable("route_models");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.FallbackModelId).HasColumnName("fallback_model_id");
        builder.Property(e => e.RoutingModelId).HasColumnName("routing_model_id");
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.FallbackModel).WithMany().HasForeignKey(e => e.FallbackModelId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.RoutingModel).WithMany().HasForeignKey(e => e.RoutingModelId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.Targets).WithOne(e => e.RouteModel).HasForeignKey(e => e.RouteModelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.Name).IsUnique();
    }
}

public class RouteRuleConfiguration : IEntityTypeConfiguration<RouteRule>
{
    public void Configure(EntityTypeBuilder<RouteRule> builder)
    {
        builder.ToTable("route_rules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RouteModelId).HasColumnName("route_model_id");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Condition).HasColumnName("condition").HasMaxLength(5000).IsRequired();
        builder.Property(e => e.TargetModelId).HasColumnName("target_model_id");
        builder.Property(e => e.Priority).HasColumnName("priority");
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.RouteModel).WithMany(e => e.Rules).HasForeignKey(e => e.RouteModelId);
        builder.HasOne(e => e.TargetModel).WithMany().HasForeignKey(e => e.TargetModelId);
    }
}

public class RouteModelTargetConfiguration : IEntityTypeConfiguration<RouteModelTarget>
{
    public void Configure(EntityTypeBuilder<RouteModelTarget> builder)
    {
        builder.ToTable("route_model_targets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RouteModelId).HasColumnName("route_model_id");
        builder.Property(e => e.ModelId).HasColumnName("model_id");
        builder.Property(e => e.IsActive).HasColumnName("is_active");
        builder.Property(e => e.Priority).HasColumnName("priority");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne(e => e.RouteModel).WithMany(e => e.Targets).HasForeignKey(e => e.RouteModelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Model).WithMany().HasForeignKey(e => e.ModelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.RouteModelId, e.ModelId }).IsUnique();
    }
}

public class RequestLogConfiguration : IEntityTypeConfiguration<RequestLog>
{
    public void Configure(EntityTypeBuilder<RequestLog> builder)
    {
        builder.ToTable("request_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.RequestId).HasColumnName("request_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Timestamp).HasColumnName("timestamp");
        builder.Property(e => e.ApiKeyId).HasColumnName("api_key_id");
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ResolvedModelName).HasColumnName("resolved_model_name").HasMaxLength(200);
        builder.Property(e => e.ProviderId).HasColumnName("provider_id");
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(200);
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens");
        builder.Property(e => e.InputTokensAfterCompression).HasColumnName("input_tokens_after_compression");
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        builder.Property(e => e.CacheHit).HasColumnName("cache_hit");
        builder.Property(e => e.SemanticCacheHit).HasColumnName("semantic_cache_hit");
        builder.Property(e => e.TimeToFirstTokenMs).HasColumnName("time_to_first_token_ms");
        builder.Property(e => e.TotalDurationMs).HasColumnName("total_duration_ms");
        builder.Property(e => e.OutputTokensPerSecond).HasColumnName("output_tokens_per_second").HasPrecision(10, 2);
        builder.Property(e => e.InputCost).HasColumnName("input_cost").HasPrecision(18, 8);
        builder.Property(e => e.OutputCost).HasColumnName("output_cost").HasPrecision(18, 8);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(e => e.CompressionApplied).HasColumnName("compression_applied");
        builder.Property(e => e.CompressionStrategy).HasColumnName("compression_strategy").HasMaxLength(100);
        builder.Property(e => e.CompressionMappingKey).HasColumnName("compression_mapping_key").HasMaxLength(100);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(50);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(e => e.RetryCount).HasColumnName("retry_count");
        builder.Property(e => e.IsStream).HasColumnName("is_stream");
        builder.Property(e => e.RequestContent).HasColumnName("request_content");
        builder.Property(e => e.ResponseContent).HasColumnName("response_content");

        builder.HasIndex(e => e.RequestId).IsUnique();
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.ModelName);
    }
}

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Value).HasColumnName("value").HasMaxLength(4000).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Key).IsUnique();
    }
}

public class ArchivedRequestLogConfiguration : IEntityTypeConfiguration<ArchivedRequestLog>
{
    public void Configure(EntityTypeBuilder<ArchivedRequestLog> builder)
    {
        builder.ToTable("archived_request_logs");
        builder.HasKey(e => e.RequestId);
        builder.Property(e => e.RequestId).HasColumnName("request_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Timestamp).HasColumnName("timestamp");
        builder.Property(e => e.ApiKeyId).HasColumnName("api_key_id");
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ResolvedModelName).HasColumnName("resolved_model_name").HasMaxLength(200);
        builder.Property(e => e.ProviderId).HasColumnName("provider_id");
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(200);
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens");
        builder.Property(e => e.InputTokensAfterCompression).HasColumnName("input_tokens_after_compression");
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        builder.Property(e => e.CacheHit).HasColumnName("cache_hit");
        builder.Property(e => e.SemanticCacheHit).HasColumnName("semantic_cache_hit");
        builder.Property(e => e.TimeToFirstTokenMs).HasColumnName("time_to_first_token_ms");
        builder.Property(e => e.TotalDurationMs).HasColumnName("total_duration_ms");
        builder.Property(e => e.OutputTokensPerSecond).HasColumnName("output_tokens_per_second").HasPrecision(10, 2);
        builder.Property(e => e.InputCost).HasColumnName("input_cost").HasPrecision(18, 8);
        builder.Property(e => e.OutputCost).HasColumnName("output_cost").HasPrecision(18, 8);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(e => e.CompressionApplied).HasColumnName("compression_applied");
        builder.Property(e => e.CompressionStrategy).HasColumnName("compression_strategy").HasMaxLength(100);
        builder.Property(e => e.CompressionMappingKey).HasColumnName("compression_mapping_key").HasMaxLength(100);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(50);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(e => e.RetryCount).HasColumnName("retry_count");
        builder.Property(e => e.IsStream).HasColumnName("is_stream");
        builder.Property(e => e.RequestContent).HasColumnName("request_content");
        builder.Property(e => e.ResponseContent).HasColumnName("response_content");

        builder.HasIndex(e => e.RequestId).IsUnique();
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.ModelName);
    }
}

public class OAuthProviderConfiguration : IEntityTypeConfiguration<OAuthProvider>
{
    public void Configure(EntityTypeBuilder<OAuthProvider> builder)
    {
        builder.ToTable("oauth_providers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(e => e.Protocol).HasColumnName("protocol").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("client_id").HasMaxLength(500).IsRequired();
        builder.Property(e => e.ClientSecret).HasColumnName("client_secret").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.AuthorizationEndpoint).HasColumnName("authorization_endpoint").HasMaxLength(1000);
        builder.Property(e => e.TokenEndpoint).HasColumnName("token_endpoint").HasMaxLength(1000);
        builder.Property(e => e.UserInfoEndpoint).HasColumnName("userinfo_endpoint").HasMaxLength(1000);
        builder.Property(e => e.Issuer).HasColumnName("issuer").HasMaxLength(500);
        builder.Property(e => e.Scope).HasColumnName("scope").HasMaxLength(500).IsRequired();
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Name).IsUnique();
    }
}

public class ModelCapabilityConfiguration : IEntityTypeConfiguration<ModelCapability>
{
    public void Configure(EntityTypeBuilder<ModelCapability> builder)
    {
        builder.ToTable("model_capabilities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ModelId).HasColumnName("model_id");
        builder.Property(e => e.Dimension).HasColumnName("dimension").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Score).HasColumnName("score").HasPrecision(6, 2);
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Evidence).HasColumnName("evidence").HasMaxLength(2000);
        builder.Property(e => e.EvaluatedAt).HasColumnName("evaluated_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Model).WithMany(m => m.Capabilities).HasForeignKey(e => e.ModelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ModelId, e.Dimension }).IsUnique();
    }
}

public class DataDeletionRequestConfiguration : IEntityTypeConfiguration<DataDeletionRequest>
{
    public void Configure(EntityTypeBuilder<DataDeletionRequest> builder)
    {
        builder.ToTable("data_deletion_requests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.ApiKeyId).HasColumnName("api_key_id").HasMaxLength(100);
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.DeletedRequestLogs).HasColumnName("deleted_request_logs");
        builder.Property(e => e.DeletedArchivedLogs).HasColumnName("deleted_archived_logs");
        builder.Property(e => e.RequestId).HasColumnName("request_id").HasMaxLength(100);

        builder.HasIndex(e => e.RequestId).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);
    }
}

public class DailyStatConfiguration : IEntityTypeConfiguration<DailyStat>
{
    public void Configure(EntityTypeBuilder<DailyStat> builder)
    {
        builder.ToTable("daily_stats");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Date).HasColumnName("date");
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.ModelName).HasColumnName("model_name").HasMaxLength(200);
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(200);
        builder.Property(e => e.TotalRequests).HasColumnName("total_requests");
        builder.Property(e => e.SuccessRequests).HasColumnName("success_requests");
        builder.Property(e => e.FailedRequests).HasColumnName("failed_requests");
        builder.Property(e => e.RateLimitedRequests).HasColumnName("rate_limited_requests");
        builder.Property(e => e.CacheHits).HasColumnName("cache_hits");
        builder.Property(e => e.TotalInputTokens).HasColumnName("total_input_tokens");
        builder.Property(e => e.TotalOutputTokens).HasColumnName("total_output_tokens");
        builder.Property(e => e.TotalInputTokensAfterCompression).HasColumnName("total_input_tokens_after_compression");
        builder.Property(e => e.TotalInputCost).HasColumnName("total_input_cost").HasPrecision(18, 8);
        builder.Property(e => e.TotalOutputCost).HasColumnName("total_output_cost").HasPrecision(18, 8);
        builder.Property(e => e.AvgLatencyMs).HasColumnName("avg_latency_ms");
        builder.Property(e => e.P50LatencyMs).HasColumnName("p50_latency_ms");
        builder.Property(e => e.P95LatencyMs).HasColumnName("p95_latency_ms");
        builder.Property(e => e.P99LatencyMs).HasColumnName("p99_latency_ms");
        builder.Property(e => e.AvgTtftMs).HasColumnName("avg_ttft_ms");
        builder.Property(e => e.AvgOutputTokensPerSecond).HasColumnName("avg_output_tokens_per_second").HasPrecision(10, 2);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => new { e.Date, e.OrganizationId, e.ModelName, e.ProviderName }).IsUnique();
        builder.HasIndex(e => e.Date);
    }
}

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("admin_audit_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Timestamp).HasColumnName("timestamp");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.Username).HasColumnName("username").HasMaxLength(100);
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").HasMaxLength(100);
        builder.Property(e => e.Details).HasColumnName("details").HasMaxLength(5000);
        builder.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
        builder.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);

        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Action);
    }
}

public class WebhookNotificationConfiguration : IEntityTypeConfiguration<WebhookNotification>
{
    public void Configure(EntityTypeBuilder<WebhookNotification> builder)
    {
        builder.ToTable("webhook_notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Secret).HasColumnName("secret").HasMaxLength(500);
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.Events).HasColumnName("events").HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.WebhookNotificationId).HasColumnName("webhook_notification_id");
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasMaxLength(8000);
        builder.Property(e => e.AttemptCount).HasColumnName("attempt_count");
        builder.Property(e => e.MaxAttempts).HasColumnName("max_attempts");
        builder.Property(e => e.LastStatusCode).HasColumnName("last_status_code").HasMaxLength(20);
        builder.Property(e => e.LastErrorMessage).HasColumnName("last_error_message").HasMaxLength(2000);
        builder.Property(e => e.NextRetryAt).HasColumnName("next_retry_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(e => e.IsSuccess).HasColumnName("is_success");

        builder.HasIndex(e => new { e.WebhookNotificationId, e.IsSuccess });
        builder.HasIndex(e => e.NextRetryAt);
    }
}

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("alert_rules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Severity).HasColumnName("severity").HasMaxLength(20);
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.WebhookIds).HasColumnName("webhook_ids").HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}
