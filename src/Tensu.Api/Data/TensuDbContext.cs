using Microsoft.EntityFrameworkCore;
using Tensu.Core.Entities;

namespace Tensu.Api.Data;

public class TensuDbContext : DbContext
{
    public TensuDbContext(DbContextOptions<TensuDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OAuthProvider> OAuthProviders => Set<OAuthProvider>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ProviderKey> ProviderKeys => Set<ProviderKey>();
    public DbSet<Model> Models => Set<Model>();
    public DbSet<ModelPricing> ModelPricings => Set<ModelPricing>();
    public DbSet<ModelCapability> ModelCapabilities => Set<ModelCapability>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Quota> Quotas => Set<Quota>();
    public DbSet<RateLimitCounter> RateLimitCounters => Set<RateLimitCounter>();
    public DbSet<RouteModel> RouteModels => Set<RouteModel>();
    public DbSet<RouteModelTarget> RouteModelTargets => Set<RouteModelTarget>();
    public DbSet<RouteRule> RouteRules => Set<RouteRule>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
    public DbSet<ArchivedRequestLog> ArchivedRequestLogs => Set<ArchivedRequestLog>();
    public DbSet<DataDeletionRequest> DataDeletionRequests => Set<DataDeletionRequest>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<CompressionMapping> CompressionMappings => Set<CompressionMapping>();
    public DbSet<SemanticCacheEntry> SemanticCacheEntries => Set<SemanticCacheEntry>();
    public DbSet<ExactCacheEntry> ExactCacheEntries => Set<ExactCacheEntry>();
    public DbSet<DailyStat> DailyStats => Set<DailyStat>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<WebhookNotification> WebhookNotifications => Set<WebhookNotification>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public override int SaveChanges()
    {
        ValidateAuditImmutability();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateAuditImmutability();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ValidateAuditImmutability()
    {
        var modifiedEntities = ChangeTracker.Entries()
            .Where(e => e.Entity is RequestLog or ArchivedRequestLog or AdminAuditLog)
            .ToList();

        foreach (var entry in modifiedEntities)
        {
            if (entry.State == EntityState.Modified)
                throw new InvalidOperationException("Audit logs are immutable and cannot be modified.");
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Audit logs are immutable and cannot be deleted directly.");
        }
    }

    public async Task EnsureAuditTriggersAsync()
    {
        var triggerSql = @"
            CREATE TRIGGER IF NOT EXISTS trg_request_logs_no_update
                BEFORE UPDATE ON request_logs
            BEGIN
                SELECT RAISE(ABORT, 'request_logs is immutable');
            END;

            CREATE TRIGGER IF NOT EXISTS trg_request_logs_no_delete
                BEFORE DELETE ON request_logs
            BEGIN
                SELECT RAISE(ABORT, 'request_logs is immutable');
            END;

            CREATE TRIGGER IF NOT EXISTS trg_archived_request_logs_no_update
                BEFORE UPDATE ON archived_request_logs
            BEGIN
                SELECT RAISE(ABORT, 'archived_request_logs is immutable');
            END;

            CREATE TRIGGER IF NOT EXISTS trg_archived_request_logs_no_delete
                BEFORE DELETE ON archived_request_logs
            BEGIN
                SELECT RAISE(ABORT, 'archived_request_logs is immutable');
            END;

            CREATE TRIGGER IF NOT EXISTS trg_admin_audit_logs_no_update
                BEFORE UPDATE ON admin_audit_logs
            BEGIN
                SELECT RAISE(ABORT, 'admin_audit_logs is immutable');
            END;

            CREATE TRIGGER IF NOT EXISTS trg_admin_audit_logs_no_delete
                BEFORE DELETE ON admin_audit_logs
            BEGIN
                SELECT RAISE(ABORT, 'admin_audit_logs is immutable');
            END;
        ";

        await Database.ExecuteSqlRawAsync(triggerSql);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TensuDbContext).Assembly);
    }
}
