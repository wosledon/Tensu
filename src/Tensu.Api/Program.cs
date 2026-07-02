using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// EF Core - SQLite for dev, PostgreSQL for production
var dbProvider = builder.Configuration.GetValue("Database:Provider", "SQLite");
if (dbProvider == "PostgreSQL")
{
    builder.Services.AddDbContext<TensuDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));
}
else
{
    builder.Services.AddDbContext<TensuDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("SQLite") ?? "Data Source=tensu.db"));
}

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "TensuDefaultJwtSecretKey32Chars!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Tensu",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Tensu",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// Infrastructure
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<AuditChannel>();
builder.Services.AddSingleton<LoadBalancer>();
builder.Services.AddSingleton<RateLimiter>();
builder.Services.AddSingleton<RetryPolicy>();
builder.Services.AddSingleton<CompressionService>();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddHostedService<AuditBackgroundService>();

// Business services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProviderService>();
builder.Services.AddScoped<ModelService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<RouteModelService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddHostedService<HealthCheckBackgroundService>();
builder.Services.AddHttpClient();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// CORS for frontend dev
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
              .WithExposedHeaders("X-Request-Id", "X-Cache", "X-Route-Model", "X-Upstream-Provider");
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
    db.Database.EnsureCreated();

    if (!db.Users.Any())
    {
        var org = new Tensu.Core.Entities.Organization { Name = "Default", Path = "/", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Organizations.Add(org);
        db.SaveChanges();
        org.Path = $"/{org.Id}/";
        db.SaveChanges();

        db.Users.Add(new Tensu.Core.Entities.User
        {
            OrganizationId = org.Id,
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Email = "admin@tensu.local",
            DisplayName = "Administrator",
            Role = Tensu.Core.Enums.UserRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready", timestamp = DateTime.UtcNow }));
app.MapGet("/metrics", () =>
{
    var collector = app.Services.GetRequiredService<MetricsCollector>();
    return Results.Text(collector.Export(), "text/plain; version=0.0.4; charset=utf-8");
});

app.Run();
