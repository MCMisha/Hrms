using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Hrms.PeopleService.IntegrationTests;

public class PeopleApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("hrms_people_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PeopleDbContext>>();
            services.RemoveAll<PeopleDbContext>();

            services.AddDbContext<PeopleDbContext>(options =>
                options.UseNpgsql(_postgresContainer.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _ = CreateClient();

        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<PeopleDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        Dispose();

        await _postgresContainer.DisposeAsync();
    }
}