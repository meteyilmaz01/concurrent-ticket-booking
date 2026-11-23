using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using TicketBooking.Infrastructure.Persistence;

namespace TicketBooking.IntegrationTests;

// Starts a throwaway PostgreSQL in Docker and runs the API in memory against it.
public class TicketBookingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.7").Build();

    public async ValueTask InitializeAsync()
    {
        // 1) Start the database container
        await _postgres.StartAsync();

        // 2) Create the tables and the demo event with 100 seats
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SeedData.AddDemoEventAsync(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" instead of "Development": the API must not read the developer's
        // user-secrets connection string or touch the development database.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());

        // Keep test output readable: no API/EF logs
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
