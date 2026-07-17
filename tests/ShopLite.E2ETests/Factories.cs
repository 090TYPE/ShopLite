using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrderService.Data;
using UserService.Data;

namespace ShopLite.E2ETests;

// Маркерные типы вместо Program: у обоих сервисов сгенерированный Program лежит
// в глобальном пространстве имён, и этот проект ссылается на оба сразу.
public class UserApiFactory(string connectionString) : WebApplicationFactory<UserService.ApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<UserDbContext>>();
            services.RemoveAll<UserDbContext>();
            services.AddDbContext<UserDbContext>(opt => opt.UseNpgsql(connectionString));
        });
    }
}

public class OrderApiFactory(string connectionString, Uri rabbitUri)
    : WebApplicationFactory<OrderService.ApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.RemoveAll<OrderDbContext>();
            services.AddDbContext<OrderDbContext>(opt => opt.UseNpgsql(connectionString));

            // Настоящий Rabbit из контейнера вместо хоста из appsettings.
            // AddMassTransitTestHarness, а не AddMassTransit: он единственный, кто
            // снимает уже сделанную в Program.cs регистрацию Rabbit-шины (публичного
            // RemoveMassTransit в 8.3.5 нет — метод internal). Транспорт при этом
            // настоящий: харнесс лишь конфигурирует шину, UsingRabbitMq остаётся в силе.
            services.AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderCreatedSpy>();
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Host(rabbitUri);
                    cfg.ConfigureEndpoints(ctx);
                });
            });
        });
    }
}
