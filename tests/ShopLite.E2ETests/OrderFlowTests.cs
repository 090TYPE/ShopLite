using FluentAssertions;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace ShopLite.E2ETests;

[Collection(InfrastructureCollection.Name)]
public class OrderFlowTests(InfrastructureFixture infra)
{
    private record JsonOrder(Guid Id, decimal Total);

    [Fact]
    public async Task Registered_user_places_order_and_event_reaches_the_consumer()
    {
        await using var users = new UserApiFactory(infra.UserDbConnectionString);
        await using var orders = new OrderApiFactory(infra.OrderDbConnectionString, infra.RabbitUri);

        // 1. Регистрация
        using var userClient = users.CreateClient();
        var registration = await userClient.PostAsJsonAsync("/api/auth/register",
            new { Email = "e2e@example.com", Password = "s3cret!", Name = "E2E" });
        registration.IsSuccessStatusCode.Should().BeTrue();

        // 2. Логин — токен настоящий, выпущен UserService
        var login = await userClient.PostAsJsonAsync("/api/auth/login",
            new { Email = "e2e@example.com", Password = "s3cret!" });
        var token = (await login.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["token"];

        // 3. Заказ
        using var orderClient = orders.CreateClient();
        orderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Публикация внутри обработчика блокируется, если брокер недоступен, и
        // ждёт вечно — без таймаута тест повесил бы CI вместо того, чтобы упасть.
        orderClient.Timeout = TimeSpan.FromSeconds(30);

        var created = await orderClient.PostAsJsonAsync("/api/orders",
            new { Items = new[] { new { ProductName = "Keyboard", Quantity = 2, Price = 49.50m } } });
        created.IsSuccessStatusCode.Should().BeTrue();
        var order = await created.Content.ReadFromJsonAsync<JsonOrder>();

        // 4. Событие доехало через настоящий Rabbit до консьюмера
        var delivered = await OrderCreatedSpy.Delivered.WaitAsync(TimeSpan.FromSeconds(30));

        delivered.OrderId.Should().Be(order!.Id);
        delivered.Total.Should().Be(99.00m);
    }
}
