using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Tensu.Api.Controllers;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Enums;
using Xunit;
using Microsoft.AspNetCore.Hosting;

namespace Tensu.Tests.Integration;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly WebApplicationFactory<AlertRulesController> _factory;

    protected IntegrationTestBase()
    {
        _factory = new WebApplicationFactory<AlertRulesController>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:Provider"] = "InMemory",
                        ["Jwt:Key"] = "TestJwtSecretKey32CharsForSigning!!",
                        ["Jwt:Issuer"] = "Tensu",
                        ["Jwt:Audience"] = "Tensu",
                        ["Jwt:ExpiresInMinutes"] = "60",
                        ["Encryption:Key"] = "TestKey32BytesForAES256Encryption!!",
                        ["Encryption:IV"] = "TestIV16Bytes!!"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TensuDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<TensuDbContext>(options =>
                        options.UseInMemoryDatabase("TensuIntegrationTestDb"));
                });
            });

        Client = _factory.CreateClient();
    }

    protected HttpClient Client { get; }

    protected async Task<string> GetAuthTokenAsync(string username = "testadmin", string role = "Admin", int orgId = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var jwtService = new JwtService(configuration);
        return jwtService.GenerateToken(1, username, role, orgId);
    }

    protected async Task SetAuthHeaderAsync(string username = "testadmin", string role = "Admin", int orgId = 1)
    {
        var token = await GetAuthTokenAsync(username, role, orgId);
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
    }

    public virtual async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await Task.CompletedTask;
    }
}
