using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace OrderService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class OrderEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid UserId = Guid.NewGuid();

    private OrderApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new OrderApiFactory(postgres.ConnectionString);
        await _factory.ResetDatabaseAsync();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.ForUser(UserId));
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private record ItemPayload(string ProductName, int Quantity, decimal Price);
    private record OrderPayload(List<ItemPayload> Items);

    private static OrderPayload ValidPayload()
        => new([new ItemPayload("Keyboard", 2, 49.50m)]);

    [Fact]
    public async Task Post_order_returns_201_and_row_lands_in_database()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.Include(o => o.Items).SingleAsync();

        order.UserId.Should().Be(UserId);
        order.Total.Should().Be(99.00m);
        order.Items.Should().ContainSingle(i => i.ProductName == "Keyboard");
    }

    [Fact]
    public async Task Post_order_without_token_is_401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/orders", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_order_with_no_items_is_400_and_saves_nothing()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new OrderPayload([]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Post_order_with_negative_price_is_400()
    {
        var payload = new OrderPayload([new ItemPayload("Keyboard", 1, -5m)]);

        var response = await _client.PostAsJsonAsync("/api/orders", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_order_with_zero_quantity_is_400()
    {
        var payload = new OrderPayload([new ItemPayload("Keyboard", 0, 5m)]);

        var response = await _client.PostAsJsonAsync("/api/orders", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_orders_returns_only_callers_orders()
    {
        await _client.PostAsJsonAsync("/api/orders", ValidPayload());

        using var stranger = _factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.ForUser(Guid.NewGuid()));

        var mine = await _client.GetFromJsonAsync<List<JsonOrder>>("/api/orders");
        var theirs = await stranger.GetFromJsonAsync<List<JsonOrder>>("/api/orders");

        mine.Should().HaveCount(1);
        theirs.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_order_by_id_returns_it()
    {
        var created = await _client.PostAsJsonAsync("/api/orders", ValidPayload());
        var id = (await created.Content.ReadFromJsonAsync<JsonOrder>())!.Id;

        var response = await _client.GetAsync($"/api/orders/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_another_users_order_is_404()
    {
        var created = await _client.PostAsJsonAsync("/api/orders", ValidPayload());
        var id = (await created.Content.ReadFromJsonAsync<JsonOrder>())!.Id;

        using var stranger = _factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.ForUser(Guid.NewGuid()));

        var response = await stranger.GetAsync($"/api/orders/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_unknown_order_is_404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record JsonOrder(Guid Id, decimal Total);
}
