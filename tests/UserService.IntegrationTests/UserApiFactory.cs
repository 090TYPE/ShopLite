using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using UserService;
using UserService.Data;

namespace UserService.IntegrationTests;

public class UserApiFactory(string connectionString) : WebApplicationFactory<ApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Program.cs регистрирует DbContext на строку из appsettings (Host=user-db),
            // которая в тестах недоступна — подменяем на контейнер.
            services.RemoveAll<DbContextOptions<UserDbContext>>();
            services.RemoveAll<UserDbContext>();

            services.AddDbContext<UserDbContext>(opt => opt.UseNpgsql(connectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
}
