using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tensu.Api.Data;
using Xunit;

namespace Tensu.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<object>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
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
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        });
    }
}
