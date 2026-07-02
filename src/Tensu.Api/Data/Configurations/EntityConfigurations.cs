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
        builder.Property(e => e.Rpm).HasColumnName("rpm");
        builder.Property(e => e.Tpm).HasColumnName("tpm");
        builder.Property(e => e.DailyTokenLimit).HasColumnName("daily_token_limit");
        builder.Property(e => e.MonthlyTokenLimit).HasColumnName("monthly_token_limit");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.Organization).WithMany(e => e.Quotas).HasForeignKey(e => e.OrganizationId);
        builder.HasOne(e => e.ApiKey).WithMany().HasForeignKey(e => e.ApiKeyId).OnDelete(DeleteBehavior.SetNull);
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
        builder.Property(e => e.TargetModelId).HasColumnName("target_model_id");
        builder.Property(e => e.FallbackModelId).HasColumnName("fallback_model_id");
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(e => e.TargetModel).WithMany().HasForeignKey(e => e.TargetModelId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.FallbackModel).WithMany().HasForeignKey(e => e.FallbackModelId).OnDelete(DeleteBehavior.SetNull);
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
        builder.Property(e => e.TimeToFirstTokenMs).HasColumnName("time_to_first_token_ms");
        builder.Property(e => e.TotalDurationMs).HasColumnName("total_duration_ms");
        builder.Property(e => e.OutputTokensPerSecond).HasColumnName("output_tokens_per_second").HasPrecision(10, 2);
        builder.Property(e => e.InputCost).HasColumnName("input_cost").HasPrecision(18, 8);
        builder.Property(e => e.OutputCost).HasColumnName("output_cost").HasPrecision(18, 8);
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(e => e.CompressionApplied).HasColumnName("compression_applied");
        builder.Property(e => e.CompressionStrategy).HasColumnName("compression_strategy").HasMaxLength(100);
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
