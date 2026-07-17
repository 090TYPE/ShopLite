using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace ShopLite.E2ETests;

public class InfrastructureFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _userDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("users").Build();

    private readonly PostgreSqlContainer _orderDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("orders").Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine").Build();

    public string UserDbConnectionString => _userDb.GetConnectionString();
    public string OrderDbConnectionString => _orderDb.GetConnectionString();

    /// <summary>
    /// amqp://rabbitmq:rabbitmq@host:port — MassTransit конфигурируется этим URI.
    /// Учётные данные задаёт модуль Testcontainers, и это не guest/guest.
    /// </summary>
    public Uri RabbitUri => new(_rabbit.GetConnectionString());

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_userDb.StartAsync(), _orderDb.StartAsync(), _rabbit.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _userDb.DisposeAsync().AsTask(),
            _orderDb.DisposeAsync().AsTask(),
            _rabbit.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public class InfrastructureCollection : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "infrastructure";
}
