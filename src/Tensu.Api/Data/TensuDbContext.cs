using Microsoft.EntityFrameworkCore;
using Tensu.Core.Entities;

namespace Tensu.Api.Data;

public class TensuDbContext : DbContext
{
    public TensuDbContext(DbContextOptions<TensuDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ProviderKey> ProviderKeys => Set<ProviderKey>();
    public DbSet<Model> Models => Set<Model>();
    public DbSet<ModelPricing> ModelPricings => Set<ModelPricing>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Quota> Quotas => Set<Quota>();
    public DbSet<RateLimitCounter> RateLimitCounters => Set<RateLimitCounter>();
    public DbSet<RouteModel> RouteModels => Set<RouteModel>();
    public DbSet<RouteRule> RouteRules => Set<RouteRule>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
    public DbSet<ArchivedRequestLog> ArchivedRequestLogs => Set<ArchivedRequestLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<CompressionMapping> CompressionMappings => Set<CompressionMapping>();
    public DbSet<SemanticCacheEntry> SemanticCacheEntries => Set<SemanticCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TensuDbContext).Assembly);
    }
}
