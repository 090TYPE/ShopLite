using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrderService;
using OrderService.Data;

namespace OrderService.IntegrationTests;

public class OrderApiFactory(string connectionString) : WebApplicationFactory<ApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.RemoveAll<OrderDbContext>();
            services.AddDbContext<OrderDbContext>(opt => opt.UseNpgsql(connectionString));

            // Rabbit из Program.cs недоступен и не является предметом этих тестов —
            // заменяем транспорт на in-memory harness. Реальный брокер проверяется в E2E.
            // AddMassTransitTestHarness сам вызывает внутренний RemoveMassTransit,
            // снимая регистрации Rabbit-шины: отдельный вызов не нужен и не публичен.
            services.AddMassTransitTestHarness();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
}
