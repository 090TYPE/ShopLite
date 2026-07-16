# ShopLite Этап 1 — доменный слой и тесты: план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Извлечь доменный слой заказа с валидацией и переходами статусов, затем покрыть его unit-тестами, каждый эндпоинт — интеграционным тестом на реальном PostgreSQL, и один E2E-сценарий — сквозной доставкой события через RabbitMQ.

**Architecture:** Домен живёт в `OrderService/Domain/` без отдельной сборки. `Order.Create` — единственная точка рождения заказа, возвращает `Result<Order>` (не исключение: невалидный ввод ожидаем). Контроллер маппит `Result.Failure` в 400. Интеграционные тесты поднимают Postgres через Testcontainers и подменяют `DbContext` в `WebApplicationFactory`; брокер там in-memory (MassTransit.TestFramework). Реальный RabbitMQ — только в E2E.

**Tech Stack:** .NET 10, EF Core 10, MassTransit 8.3.5, xunit, FluentAssertions 7, NSubstitute, Testcontainers, coverlet.

**Спека:** `docs/superpowers/specs/2026-07-16-shoplite-stage1-tests-design.md`

---

## Критично для окружения

**Все команды `dotnet` запускать из `C:\Users\090\Documents\GitHub\ShopLite`.** В соседнем репозитории CryptoAI лежит `global.json`, пиннящий SDK 8.0.422; из чужого каталога резолвится он и сборка падает с `NETSDK1045`. Из каталога ShopLite резолвится SDK 10.0.300 и всё собирается чисто.

В PowerShell-инструменте рабочий каталог сбрасывается между вызовами, поэтому каждая команда начинается с `Set-Location`.

**FluentAssertions строго 7.x.** Начиная с 8.0 лицензия платная для коммерческого использования; 7.2.0 — последняя под Apache 2.0. Портфельному репозиторию нужна именно она.

**Репозиторий переведён на .NET 10** (задача 2b, коммит `5cc5272`). Изначально он был на net9.0, но выяснилось, что рантайма .NET 9 на машине нет вообще — только 8 и 10. Сборка проходила (SDK 10 компилирует под net9.0 по reference-пакетам), а всё исполняемое падало с `Framework: 'Microsoft.NETCore.App', version '9.0.0' not found`. Плюс .NET 9 — STS-релиз, снятый с поддержки в мае 2026. Все проекты теперь `net10.0`, Microsoft-пакеты на 10.x, Dockerfile'ы на `sdk:10.0` / `aspnet:10.0` (у NotificationService — `runtime:10.0`, он воркер без HTTP).

**Docker Desktop должен быть запущен** начиная с Task 7 — Testcontainers без него не поднимется. Проверка: `docker version --format '{{.Server.Version}}'` (на машине подтверждён 29.5.2).

---

## Структура файлов

**Создаются:**

| Файл | Ответственность |
|---|---|
| `OrderService/Domain/Result.cs` | `Result<T>` и `Error` — транспорт исхода валидации |
| `OrderService/Domain/OrderErrors.cs` | каталог доменных ошибок заказа |
| `OrderService/Domain/NewOrderItem.cs` | входная позиция для фабрики (не EF-сущность) |
| `UserService/Services/IJwtTokenGenerator.cs` | контракт выпуска токена |
| `UserService/Services/JwtTokenGenerator.cs` | реализация, вынесенная из контроллера |
| `UserService/ApiMarker.cs` | публичный тип для привязки `WebApplicationFactory` |
| `OrderService/ApiMarker.cs` | то же для сервиса заказов |
| `tests/OrderService.UnitTests/` | правила домена без инфраструктуры |
| `tests/UserService.UnitTests/` | генератор JWT, хеш пароля |
| `tests/OrderService.IntegrationTests/` | API заказов на реальном Postgres |
| `tests/UserService.IntegrationTests/` | API аутентификации на реальном Postgres |
| `tests/ShopLite.E2ETests/` | сквозной сценарий через настоящий брокер |

**Модифицируются:**

| Файл | Что меняется |
|---|---|
| `OrderService/Models/Order.cs` | анемичная модель → агрегат с инвариантами |
| `OrderService/Data/OrderDbContext.cs` | маппинг backing field для `Items` |
| `OrderService/Controllers/OrdersController.cs` | тело `Create` → вызов фабрики + маппинг ошибки |
| `UserService/Program.cs` | регистрация `IJwtTokenGenerator` в DI |
| `UserService/Controllers/AuthController.cs` | `GenerateJwt` удаляется, инжектится генератор |
| `ShopLite.sln` | добавление пяти тестовых проектов |

---

## Task 1: Каркас `OrderService.UnitTests`

**Files:**
- Create: `tests/OrderService.UnitTests/OrderService.UnitTests.csproj`
- Modify: `ShopLite.sln`

- [ ] **Step 1: Создать проект и подключить к решению**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet new xunit -o tests/OrderService.UnitTests -f net10.0
dotnet sln ShopLite.sln add tests/OrderService.UnitTests/OrderService.UnitTests.csproj
dotnet add tests/OrderService.UnitTests/OrderService.UnitTests.csproj reference OrderService/OrderService.csproj
```

- [ ] **Step 2: Заменить содержимое csproj**

`dotnet new xunit` кладёт свои версии пакетов; фиксируем нужные явно. Полное содержимое `tests/OrderService.UnitTests/OrderService.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\OrderService\OrderService.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Удалить шаблонный тест**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
Remove-Item tests/OrderService.UnitTests/UnitTest1.cs
```

- [ ] **Step 4: Проверить сборку**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet build ShopLite.sln --nologo -v q
```

Ожидается: `Сборка успешно завершена. Ошибок: 0`.

- [ ] **Step 5: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add tests/OrderService.UnitTests ShopLite.sln
git commit -m "test: scaffold OrderService.UnitTests project"
```

---

## Task 2: Тип `Result<T>`

Нужен раньше домена: `Order.Create` возвращает именно его.

**Files:**
- Create: `OrderService/Domain/Result.cs`
- Test: `tests/OrderService.UnitTests/Domain/ResultTests.cs`

- [ ] **Step 1: Написать падающий тест**

Создать `tests/OrderService.UnitTests/Domain/ResultTests.cs`:

```csharp
using FluentAssertions;
using OrderService.Domain;
using Xunit;

namespace OrderService.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_exposes_value_and_no_error()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_exposes_error_and_no_value()
    {
        var error = new Error("orders.empty", "Order must contain at least one item");

        var result = Result<int>.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Accessing_value_of_failure_throws()
    {
        var result = Result<int>.Failure(new Error("x", "y"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Запустить тест и убедиться, что он падает**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.UnitTests --nologo
```

Ожидается: ошибка компиляции `CS0234` — namespace `OrderService.Domain` не существует. Это и есть красная фаза.

- [ ] **Step 3: Реализовать минимум**

Создать `OrderService/Domain/Result.cs`:

```csharp
namespace OrderService.Domain;

public readonly record struct Error(string Code, string Message);

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);
}
```

- [ ] **Step 4: Запустить тест и убедиться, что он проходит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.UnitTests --nologo
```

Ожидается: `Пройден! — не пройдено: 0, пройдено: 3`.

- [ ] **Step 5: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add OrderService/Domain/Result.cs tests/OrderService.UnitTests/Domain/ResultTests.cs
git commit -m "feat(domain): add Result type for expected validation failures"
```

---

## Task 3: Валидация `Order.Create`

Здесь закрывается действующий баг: сейчас заказ с пустыми позициями и отрицательной ценой создаётся успешно.

**Files:**
- Create: `OrderService/Domain/OrderErrors.cs`, `OrderService/Domain/NewOrderItem.cs`
- Modify: `OrderService/Models/Order.cs`
- Test: `tests/OrderService.UnitTests/Domain/OrderCreateTests.cs`

- [ ] **Step 1: Написать падающие тесты**

Создать `tests/OrderService.UnitTests/Domain/OrderCreateTests.cs`:

```csharp
using FluentAssertions;
using OrderService.Domain;
using OrderService.Models;
using Xunit;

namespace OrderService.UnitTests.Domain;

public class OrderCreateTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static NewOrderItem ValidItem(string name = "Keyboard", int qty = 2, decimal price = 49.50m)
        => new(name, qty, price);

    [Fact]
    public void Create_with_valid_items_succeeds()
    {
        var result = Order.Create(UserId, [ValidItem()]);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(UserId);
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Create_computes_total_across_items()
    {
        var result = Order.Create(UserId,
        [
            ValidItem("Keyboard", qty: 2, price: 49.50m),
            ValidItem("Mouse", qty: 3, price: 10.00m)
        ]);

        // 2 * 49.50 + 3 * 10.00
        result.Value.Total.Should().Be(129.00m);
    }

    [Fact]
    public void Create_starts_order_as_pending()
    {
        var result = Order.Create(UserId, [ValidItem()]);

        result.Value.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Create_with_no_items_fails()
    {
        var result = Order.Create(UserId, []);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.NoItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_quantity_fails(int quantity)
    {
        var result = Order.Create(UserId, [ValidItem(qty: quantity)]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.InvalidQuantity);
    }

    [Fact]
    public void Create_with_negative_price_fails()
    {
        var result = Order.Create(UserId, [ValidItem(price: -0.01m)]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.NegativePrice);
    }

    [Fact]
    public void Create_with_zero_price_succeeds()
    {
        // Бесплатная позиция допустима — правило Price >= 0, а не > 0
        var result = Order.Create(UserId, [ValidItem(price: 0m)]);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_product_name_fails(string name)
    {
        var result = Order.Create(UserId, [ValidItem(name: name)]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.BlankProductName);
    }

    [Fact]
    public void Create_with_empty_user_fails()
    {
        var result = Order.Create(Guid.Empty, [ValidItem()]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.UnknownUser);
    }
}
```

- [ ] **Step 2: Запустить и убедиться, что падает**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.UnitTests --nologo
```

Ожидается: ошибки компиляции — `NewOrderItem`, `OrderErrors`, `Order.Create` не существуют.

- [ ] **Step 3: Создать входную позицию**

Создать `OrderService/Domain/NewOrderItem.cs`:

```csharp
namespace OrderService.Domain;

/// <summary>Позиция, поданная на вход фабрике. Не EF-сущность: у неё нет identity.</summary>
public readonly record struct NewOrderItem(string ProductName, int Quantity, decimal Price);
```

- [ ] **Step 4: Создать каталог ошибок**

Создать `OrderService/Domain/OrderErrors.cs`:

```csharp
namespace OrderService.Domain;

public static class OrderErrors
{
    public static readonly Error NoItems =
        new("orders.no_items", "Order must contain at least one item.");

    public static readonly Error InvalidQuantity =
        new("orders.invalid_quantity", "Item quantity must be greater than zero.");

    public static readonly Error NegativePrice =
        new("orders.negative_price", "Item price cannot be negative.");

    public static readonly Error BlankProductName =
        new("orders.blank_product_name", "Item product name is required.");

    public static readonly Error UnknownUser =
        new("orders.unknown_user", "Order must belong to a user.");
}
```

- [ ] **Step 5: Превратить `Order` в агрегат**

Полностью заменить `OrderService/Models/Order.cs`:

```csharp
using OrderService.Domain;

namespace OrderService.Models;

public class Order
{
    private readonly List<OrderItem> _items = [];

    // Приватный конструктор для EF Core — материализация из БД минует фабрику.
    private Order() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public static Result<Order> Create(Guid userId, IReadOnlyCollection<NewOrderItem> items)
    {
        if (userId == Guid.Empty)
            return Result<Order>.Failure(OrderErrors.UnknownUser);

        if (items.Count == 0)
            return Result<Order>.Failure(OrderErrors.NoItems);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductName))
                return Result<Order>.Failure(OrderErrors.BlankProductName);

            if (item.Quantity <= 0)
                return Result<Order>.Failure(OrderErrors.InvalidQuantity);

            if (item.Price < 0)
                return Result<Order>.Failure(OrderErrors.NegativePrice);
        }

        var order = new Order { UserId = userId };

        foreach (var item in items)
        {
            order._items.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = item.Price
            });
        }

        order.Total = order._items.Sum(i => i.Price * i.Quantity);

        return Result<Order>.Success(order);
    }
}

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public enum OrderStatus { Pending, Processing, Shipped, Delivered, Cancelled }
```

- [ ] **Step 6: Запустить и убедиться, что проходит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.UnitTests --nologo
```

Ожидается: 14 пройдено (3 из `ResultTests` + 11 из `OrderCreateTests` с учётом `Theory`-кейсов). `OrdersController.cs` пока не компилируется — это чинится в Task 5; если сборка решения падает на контроллере, запускать только тестовый проект, как указано в команде.

- [ ] **Step 7: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add OrderService/Domain OrderService/Models/Order.cs tests/OrderService.UnitTests/Domain/OrderCreateTests.cs
git commit -m "feat(domain): enforce order invariants in Order.Create

Empty item lists, non-positive quantities and negative prices
previously produced orders with nonsensical totals."
```

---

## Task 4: Переходы статусов

Основа саги из Этапа 2.

**Files:**
- Modify: `OrderService/Models/Order.cs`, `OrderService/Domain/OrderErrors.cs`
- Test: `tests/OrderService.UnitTests/Domain/OrderStatusTests.cs`

- [ ] **Step 1: Написать падающие тесты**

Создать `tests/OrderService.UnitTests/Domain/OrderStatusTests.cs`:

```csharp
using FluentAssertions;
using OrderService.Domain;
using OrderService.Models;
using Xunit;

namespace OrderService.UnitTests.Domain;

public class OrderStatusTests
{
    private static Order NewOrder()
        => Order.Create(Guid.NewGuid(), [new NewOrderItem("Keyboard", 1, 10m)]).Value;

    [Fact]
    public void Pending_order_can_start_processing()
    {
        var order = NewOrder();

        var result = order.MarkProcessing();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public void Processing_order_can_ship()
    {
        var order = NewOrder();
        order.MarkProcessing();

        var result = order.MarkShipped();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void Pending_order_cannot_ship_without_processing()
    {
        var order = NewOrder();

        var result = order.MarkShipped();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.InvalidTransition);
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Cancelled_order_cannot_ship()
    {
        var order = NewOrder();
        order.Cancel();

        var result = order.MarkShipped();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.InvalidTransition);
    }

    [Fact]
    public void Pending_order_can_be_cancelled()
    {
        var order = NewOrder();

        var result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Delivered_order_cannot_be_cancelled()
    {
        var order = NewOrder();
        order.MarkProcessing();
        order.MarkShipped();
        order.MarkDelivered();

        var result = order.Cancel();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.CannotCancelDelivered);
        order.Status.Should().Be(OrderStatus.Delivered);
    }
}
```

- [ ] **Step 2: Запустить и убедиться, что падает**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.UnitTests --nologo
```

Ожидается: `MarkProcessing`, `MarkShipped`, `MarkDelivered`, `Cancel` не существуют.

- [ ] **Step 3: Добавить ошибки переходов**

Добавить в `OrderService/Domain/OrderErrors.cs` внутрь класса `OrderErrors`:

```csharp
    public static readonly Error InvalidTransition =
        new("orders.invalid_transition", "Order status transition is not allowed.");

    public static readonly Error CannotCancelDelivered =
        new("orders.cannot_cancel_delivered", "A delivered order cannot be cancelled.");
```

- [ ] **Step 4: Реализовать переходы**

Добавить в `OrderService/Models/Order.cs` внутрь класса `Order`, после метода `Create`:

```csharp
    public Result<Order> MarkProcessing() => TransitionTo(OrderStatus.Processing, OrderStatus.Pending);

    public Result<Order> MarkShipped() => TransitionTo(OrderStatus.Shipped, OrderStatus.Processing);

    public Result<Order> MarkDelivered() => TransitionTo(OrderStatus.Delivered, OrderStatus.Shipped);

    public Result<Order> Cancel()
    {
        if (Status == OrderStatus.Delivered)
            return Result<Order>.Failure(OrderErrors.CannotCancelDelivered);

        if (Status == OrderStatus.Cancelled)
            return Result<Order>.Failure(OrderErrors.InvalidTransition);

        Status = OrderStatus.Cancelled;
        return Result<Order>.Success(this);
    }

    private Result<Order> TransitionTo(OrderStatus target, OrderStatus requiredCurrent)
    {
        if (Status != requiredCurrent)
            return Result<Order>.Failure(OrderErrors.InvalidTransition);

        Status = target;
        return Result<Order>.Success(this);
    }
```

- [ ] **Step 5: Запустить и убедиться, что проходит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.UnitTests --nologo
```

Ожидается: 20 пройдено, 0 не пройдено.

- [ ] **Step 6: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add OrderService/Domain/OrderErrors.cs OrderService/Models/Order.cs tests/OrderService.UnitTests/Domain/OrderStatusTests.cs
git commit -m "feat(domain): guard order status transitions"
```

---

## Task 5: Подключить домен к EF и контроллеру

После Task 3 решение не собирается: контроллер присваивает `Items` и `Total`, которые стали приватными. Здесь чиним.

**Files:**
- Modify: `OrderService/Data/OrderDbContext.cs`, `OrderService/Controllers/OrdersController.cs`

- [ ] **Step 1: Настроить маппинг backing field**

`Items` теперь `IReadOnlyList<OrderItem>` над полем `_items`; EF Core надо явно сказать писать в поле. Полностью заменить `OrderService/Data/OrderDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();

        order.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Items — read-only проекция поля _items: EF должен писать в поле, а не в свойство.
        order.Metadata
            .FindNavigation(nameof(Models.Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 2: Перевести контроллер на фабрику**

Заменить метод `Create` в `OrderService/Controllers/OrdersController.cs` (строки 18–42) на:

```csharp
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var items = req.Items
            .Select(i => new NewOrderItem(i.ProductName, i.Quantity, i.Price))
            .ToList();

        var result = Order.Create(userId, items);
        if (!result.IsSuccess)
        {
            var error = result.Error!.Value;
            ModelState.AddModelError(error.Code, error.Message);
            return ValidationProblem(ModelState);
        }

        var order = result.Value;

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Публикуем событие — NotificationService получит его асинхронно
        await publisher.Publish(new OrderCreated(order.Id, order.UserId, order.Total, order.CreatedAt));

        return Created($"/api/orders/{order.Id}", new { order.Id, order.Total, order.Status });
    }
```

Добавить в начало файла к остальным using:

```csharp
using OrderService.Domain;
```

- [ ] **Step 3: Собрать решение**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet build ShopLite.sln --nologo -v q
```

Ожидается: `Ошибок: 0`.

- [ ] **Step 4: Прогнать unit-тесты**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.UnitTests --nologo
```

Ожидается: 20 пройдено.

- [ ] **Step 5: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add OrderService/Data/OrderDbContext.cs OrderService/Controllers/OrdersController.cs
git commit -m "refactor(orders): route creation through domain factory

Controller no longer computes totals or assigns state directly;
invalid payloads now return 400 instead of a bogus order."
```

---

## Task 6: Извлечь `JwtTokenGenerator` + `UserService.UnitTests`

**Files:**
- Create: `UserService/Services/IJwtTokenGenerator.cs`, `UserService/Services/JwtTokenGenerator.cs`, `tests/UserService.UnitTests/`
- Modify: `UserService/Controllers/AuthController.cs`, `UserService/Program.cs`, `ShopLite.sln`

- [ ] **Step 1: Создать тестовый проект**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet new xunit -o tests/UserService.UnitTests -f net10.0
dotnet sln ShopLite.sln add tests/UserService.UnitTests/UserService.UnitTests.csproj
Remove-Item tests/UserService.UnitTests/UnitTest1.cs
```

Полностью заменить `tests/UserService.UnitTests/UserService.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\UserService\UserService.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Написать падающий тест**

Создать `tests/UserService.UnitTests/Services/JwtTokenGeneratorTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using UserService.Models;
using UserService.Services;
using Xunit;

namespace UserService.UnitTests.Services;

public class JwtTokenGeneratorTests
{
    private const string Key = "super-secret-key-for-shoplite-at-least-32-chars!!";

    private static JwtTokenGenerator CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key,
                ["Jwt:Issuer"] = "ShopLite",
                ["Jwt:Audience"] = "ShopLite"
            })
            .Build();

        return new JwtTokenGenerator(config);
    }

    private static User SampleUser() => new()
    {
        Email = "ada@example.com",
        Name = "Ada",
        PasswordHash = "irrelevant"
    };

    [Fact]
    public void Generated_token_carries_user_id_as_subject()
    {
        var user = SampleUser();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(user));

        token.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
    }

    [Fact]
    public void Generated_token_carries_email_and_name()
    {
        var user = SampleUser();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(user));

        token.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Email && c.Value == "ada@example.com");
        token.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Ada");
    }

    [Fact]
    public void Generated_token_uses_configured_issuer_and_audience()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(SampleUser()));

        token.Issuer.Should().Be("ShopLite");
        token.Audiences.Should().Contain("ShopLite");
    }

    [Fact]
    public void Generated_token_expires_in_about_24_hours()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(SampleUser()));

        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Generated_token_is_signed_with_hmac_sha256()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateSut().Generate(SampleUser()));

        token.SignatureAlgorithm.Should().Be("HS256");
    }
}
```

- [ ] **Step 3: Запустить и убедиться, что падает**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/UserService.UnitTests --nologo
```

Ожидается: namespace `UserService.Services` не существует.

- [ ] **Step 4: Создать контракт**

Создать `UserService/Services/IJwtTokenGenerator.cs`:

```csharp
using UserService.Models;

namespace UserService.Services;

public interface IJwtTokenGenerator
{
    string Generate(User user);
}
```

- [ ] **Step 5: Создать реализацию**

Создать `UserService/Services/JwtTokenGenerator.cs` — тело перенесено из приватного `AuthController.GenerateJwt`:

```csharp
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Models;

namespace UserService.Services;

public class JwtTokenGenerator(IConfiguration config) : IJwtTokenGenerator
{
    public string Generate(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name)
            ],
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 6: Убрать генерацию из контроллера**

Полностью заменить `UserService/Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserDbContext db, IJwtTokenGenerator tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { message = "Email already exists" });

        var user = new User
        {
            Email = req.Email,
            Name = req.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Created($"/api/users/{user.Id}", new { user.Id, user.Email, user.Name });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials" });

        return Ok(new { token = tokens.Generate(user) });
    }
}

public record RegisterRequest(string Email, string Password, string Name);
public record LoginRequest(string Email, string Password);
```

- [ ] **Step 7: Зарегистрировать генератор в DI**

В `UserService/Program.cs` добавить после `builder.Services.AddDbContext<UserDbContext>(...)` (строка 14):

```csharp
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
```

И добавить к using в начале файла:

```csharp
using UserService.Services;
```

- [ ] **Step 8: Запустить и убедиться, что проходит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet build ShopLite.sln --nologo -v q
dotnet test tests/UserService.UnitTests --nologo
```

Ожидается: сборка без ошибок, 5 тестов пройдено.

- [ ] **Step 9: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add UserService tests/UserService.UnitTests ShopLite.sln
git commit -m "refactor(auth): extract JwtTokenGenerator behind an interface"
```

---

## Task 7: Инфраструктура интеграционных тестов

**Files:**
- Create: `tests/UserService.IntegrationTests/` (csproj, `PostgresFixture.cs`, `UserApiFactory.cs`, `SmokeTests.cs`), `UserService/ApiMarker.cs`, `OrderService/ApiMarker.cs`
- Modify: `ShopLite.sln`

- [ ] **Step 1: Добавить маркерные типы для тестовой фабрики**

`WebApplicationFactory<T>` нужен публичный тип, по сборке которого она находит точку входа. Очевидный ход — `public partial class Program;` — здесь не работает: top-level statements генерируют `Program` в глобальном пространстве имён, а `ShopLite.E2ETests` (Task 10) ссылается на оба сервиса сразу и получит два `global::Program` и `CS0433: ambiguous reference`. Перенести `Program` в namespace через `partial` нельзя — partial-класс обязан лежать в том же пространстве, что и сгенерированная часть.

Поэтому фабрика параметризуется маркерным типом, а не `Program`.

Создать `UserService/ApiMarker.cs`:

```csharp
namespace UserService;

/// <summary>
/// Точка привязки для WebApplicationFactory в тестах: по сборке этого типа
/// фабрика находит точку входа приложения. Сгенерированный Program лежит в
/// глобальном пространстве имён и в E2E конфликтует с Program другого сервиса.
/// </summary>
public sealed class ApiMarker;
```

Создать `OrderService/ApiMarker.cs`:

```csharp
namespace OrderService;

/// <summary>
/// Точка привязки для WebApplicationFactory в тестах: по сборке этого типа
/// фабрика находит точку входа приложения. Сгенерированный Program лежит в
/// глобальном пространстве имён и в E2E конфликтует с Program другого сервиса.
/// </summary>
public sealed class ApiMarker;
```

Файлы `Program.cs` обоих сервисов при этом не меняются.

- [ ] **Step 2: Создать проект**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet new xunit -o tests/UserService.IntegrationTests -f net10.0
dotnet sln ShopLite.sln add tests/UserService.IntegrationTests/UserService.IntegrationTests.csproj
Remove-Item tests/UserService.IntegrationTests/UnitTest1.cs
```

Полностью заменить `tests/UserService.IntegrationTests/UserService.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.1.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\UserService\UserService.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Написать фикстуру контейнера**

Один контейнер на всю сборку — контейнер на класс сделает прогон невыносимым. Создать `tests/UserService.IntegrationTests/PostgresFixture.cs`:

```csharp
using Testcontainers.PostgreSql;
using Xunit;

namespace UserService.IntegrationTests;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("users")
        .WithUsername("postgres")
        .WithPassword("secret")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
```

- [ ] **Step 4: Написать фабрику приложения**

Создать `tests/UserService.IntegrationTests/UserApiFactory.cs`:

```csharp
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
```

- [ ] **Step 4a: Проверить, что фабрика поднимает приложение**

Прежде чем писать тесты API, убедимся, что связка контейнер + фабрика работает. Создать `tests/UserService.IntegrationTests/SmokeTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace UserService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class SmokeTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Application_starts_and_serves_swagger()
    {
        await using var factory = new UserApiFactory(postgres.ConnectionString);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
```

- [ ] **Step 4b: Запустить smoke-тест**

Docker Desktop должен быть запущен.

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/UserService.IntegrationTests --nologo
```

Ожидается: 1 пройден. Первый прогон дольше — тянется образ `postgres:16-alpine`.

Если падает с `EnsureCreated` поверх уже созданной схемы или на резолве `DbContextOptions` — проблема в Step 4, а не в тестах: проверить, что `RemoveAll` снял обе регистрации.

- [ ] **Step 5: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add tests/UserService.IntegrationTests UserService/ApiMarker.cs OrderService/ApiMarker.cs ShopLite.sln
git commit -m "test: add Testcontainers Postgres fixture and UserService API factory"
```

---

## Task 8: Интеграционные тесты `UserService`

**Files:**
- Create: `tests/UserService.IntegrationTests/AuthEndpointsTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/UserService.IntegrationTests/AuthEndpointsTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using UserService.Data;
using Xunit;

namespace UserService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AuthEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private UserApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new UserApiFactory(postgres.ConnectionString);
        await _factory.ResetDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private record Registration(string Email, string Password, string Name);

    [Fact]
    public async Task Register_creates_user_and_persists_it()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("ada@example.com", "s3cret!", "Ada"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        db.Users.Should().ContainSingle(u => u.Email == "ada@example.com");
    }

    [Fact]
    public async Task Register_never_stores_the_raw_password()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("grace@example.com", "s3cret!", "Grace"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var user = db.Users.Single(u => u.Email == "grace@example.com");

        user.PasswordHash.Should().NotBe("s3cret!");
        user.PasswordHash.Should().StartWith("$2");  // BCrypt
    }

    [Fact]
    public async Task Register_with_duplicate_email_conflicts()
    {
        var payload = new Registration("dup@example.com", "s3cret!", "Dup");
        await _client.PostAsJsonAsync("/api/auth/register", payload);

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("login@example.com", "s3cret!", "Login"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "login@example.com", Password = "s3cret!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["token"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new Registration("wrong@example.com", "s3cret!", "Wrong"));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "wrong@example.com", Password = "not-it" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_unknown_email_is_unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "ghost@example.com", Password = "s3cret!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Запустить**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/UserService.IntegrationTests --nologo
```

Ожидается: 7 пройдено (6 + smoke).

- [ ] **Step 3: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add tests/UserService.IntegrationTests/AuthEndpointsTests.cs
git commit -m "test: cover auth endpoints against real Postgres"
```

---

## Task 9: Интеграционные тесты `OrderService`

Брокер здесь in-memory: предмет проверки — HTTP и БД, а не доставка.

**Files:**
- Create: `tests/OrderService.IntegrationTests/` (csproj, `PostgresFixture.cs`, `OrderApiFactory.cs`, `TestJwt.cs`, `OrderEndpointsTests.cs`)
- Modify: `ShopLite.sln`

- [ ] **Step 1: Создать проект**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet new xunit -o tests/OrderService.IntegrationTests -f net10.0
dotnet sln ShopLite.sln add tests/OrderService.IntegrationTests/OrderService.IntegrationTests.csproj
Remove-Item tests/OrderService.IntegrationTests/UnitTest1.cs
```

Полностью заменить `tests/OrderService.IntegrationTests/OrderService.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.1.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />
    <PackageReference Include="MassTransit.TestFramework" Version="8.3.5" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\OrderService\OrderService.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Написать фикстуру контейнера**

Создать `tests/OrderService.IntegrationTests/PostgresFixture.cs`:

```csharp
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderService.IntegrationTests;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("orders")
        .WithUsername("postgres")
        .WithPassword("secret")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
```

- [ ] **Step 3: Написать фабрику приложения**

Создать `tests/OrderService.IntegrationTests/OrderApiFactory.cs`:

```csharp
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
            services.RemoveMassTransit();
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
```

- [ ] **Step 4: Написать генератор тестовых токенов**

Тесты заказов не должны ходить в UserService — токен подписывается тем же ключом из `appsettings.json`. Создать `tests/OrderService.IntegrationTests/TestJwt.cs`:

```csharp
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OrderService.IntegrationTests;

public static class TestJwt
{
    // Совпадает с Jwt:Key в OrderService/appsettings.json.
    private const string Key = "super-secret-key-for-shoplite-at-least-32-chars!!";

    public static string ForUser(Guid userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "ShopLite",
            audience: "ShopLite",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 5: Написать тесты эндпоинтов**

Создать `tests/OrderService.IntegrationTests/OrderEndpointsTests.cs`:

```csharp
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
```

- [ ] **Step 6: Запустить**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/OrderService.IntegrationTests --nologo
```

Ожидается: 9 пройдено.

Если публикация в тестах виснет на попытке достучаться до Rabbit — подмена транспорта не сработала: проверить, что `AddMassTransitTestHarness` вызван после `RemoveMassTransit`, а не до.

- [ ] **Step 7: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add tests/OrderService.IntegrationTests ShopLite.sln
git commit -m "test: cover order endpoints against real Postgres

Includes regression tests for the validation gap: empty items,
zero quantity and negative price must not create an order."
```

---

## Task 10: E2E-сценарий

Самый ценный тест репозитория: доказывает, что событие реально проходит через брокер в другой сервис.

**Files:**
- Create: `tests/ShopLite.E2ETests/` (csproj, `RabbitFixture.cs`, `OrderFlowTests.cs`)
- Modify: `ShopLite.sln`

- [ ] **Step 1: Создать проект**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet new xunit -o tests/ShopLite.E2ETests -f net10.0
dotnet sln ShopLite.sln add tests/ShopLite.E2ETests/ShopLite.E2ETests.csproj
Remove-Item tests/ShopLite.E2ETests/UnitTest1.cs
```

Полностью заменить `tests/ShopLite.E2ETests/ShopLite.E2ETests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.1.0" />
    <PackageReference Include="Testcontainers.RabbitMq" Version="4.1.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />
    <PackageReference Include="MassTransit.RabbitMQ" Version="8.3.5" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Contracts\Contracts.csproj" />
    <ProjectReference Include="..\..\OrderService\OrderService.csproj" />
    <ProjectReference Include="..\..\UserService\UserService.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Написать фикстуру инфраструктуры**

Создать `tests/ShopLite.E2ETests/InfrastructureFixture.cs`:

```csharp
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

    /// <summary>amqp://guest:guest@host:port — MassTransit конфигурируется этим URI.</summary>
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
```

- [ ] **Step 3: Написать шпиона-консьюмера**

Настоящий `OrderCreatedConsumer` только пишет в лог, поэтому наблюдать за ним нечем. Поднимаем консьюмер с той же подпиской, который сигналит о получении. Создать `tests/ShopLite.E2ETests/OrderCreatedSpy.cs`:

```csharp
using Contracts.Events;
using MassTransit;

namespace ShopLite.E2ETests;

/// <summary>
/// Подписывается на то же событие, что и NotificationService, и делает факт
/// доставки наблюдаемым для теста.
/// </summary>
public class OrderCreatedSpy : IConsumer<OrderCreated>
{
    private static readonly TaskCompletionSource<OrderCreated> Received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static Task<OrderCreated> Delivered => Received.Task;

    public Task Consume(ConsumeContext<OrderCreated> context)
    {
        Received.TrySetResult(context.Message);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Написать фабрики сервисов**

Создать `tests/ShopLite.E2ETests/Factories.cs`:

```csharp
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
            services.RemoveMassTransit();
            services.AddMassTransit(x =>
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
```

- [ ] **Step 5: Написать сквозной тест**

Создать `tests/ShopLite.E2ETests/OrderFlowTests.cs`:

```csharp
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
```

- [ ] **Step 6: Запустить**

Docker Desktop должен быть запущен: тест поднимает три контейнера.

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test tests/ShopLite.E2ETests --nologo
```

Ожидается: 1 пройден. Первый прогон дольше — тянутся образы `postgres:16-alpine` и `rabbitmq:3.13-alpine`.

Если тест падает по таймауту на `OrderCreatedSpy.Delivered` — событие не доехало: проверить, что `cfg.Host(rabbitUri)` получает URI контейнера, а не `rabbitmq` из appsettings, и что `ConfigureEndpoints` вызван после `AddConsumer`.

- [ ] **Step 7: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add tests/ShopLite.E2ETests ShopLite.sln
git commit -m "test: end-to-end register -> login -> order -> event delivered"
```

---

## Task 11: Покрытие и README

**Files:**
- Create: `.gitignore` дополнение, `docs/coverage.md` не нужен — только README-раздел
- Modify: `README.md`, `.gitignore`

- [ ] **Step 1: Прогнать всё решение с покрытием**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet test ShopLite.sln --nologo --collect:"XPlat Code Coverage"
```

Ожидается: все проекты зелёные, суммарно 42 теста. Файлы `coverage.cobertura.xml` появляются в `tests/*/TestResults/<guid>/`.

- [ ] **Step 2: Установить генератор отчёта**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
dotnet tool install --global dotnet-reportgenerator-globaltool
```

Если инструмент уже стоит, команда сообщит об этом — не ошибка.

- [ ] **Step 3: Сгенерировать отчёт и посмотреть покрытие домена**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
reportgenerator -reports:"tests/**/TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"
Get-Content coverage-report/Summary.txt
```

Проверить строки `OrderService.Domain.*` и `OrderService.Models.Order` — цель спеки 60–70% по домену. Если ниже — добавить недостающие кейсы в unit-тесты, а не гнаться за цифрой по всему проекту.

- [ ] **Step 4: Не коммитить артефакты**

Добавить в конец `.gitignore`:

```gitignore

# Test artifacts
TestResults/
coverage-report/
```

- [ ] **Step 5: Дописать раздел в README**

Добавить в `README.md` раздел (разместить после описания запуска):

```markdown
## Тесты

```bash
dotnet test ShopLite.sln
```

Для интеграционных и E2E-тестов нужен запущенный Docker — PostgreSQL и RabbitMQ
поднимаются через Testcontainers, ничего доустанавливать не требуется.

| Проект | Что проверяет |
|---|---|
| `OrderService.UnitTests` | инварианты заказа и переходы статусов, без инфраструктуры |
| `UserService.UnitTests` | выпуск JWT |
| `OrderService.IntegrationTests` | API заказов на реальном PostgreSQL |
| `UserService.IntegrationTests` | API аутентификации на реальном PostgreSQL |
| `ShopLite.E2ETests` | сквозной путь: регистрация → заказ → событие доставлено через RabbitMQ |

Отчёт покрытия:

```bash
dotnet test ShopLite.sln --collect:"XPlat Code Coverage"
reportgenerator -reports:"tests/**/TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

### Design decisions

**Почему `Result<T>`, а не исключения.** Невалидный ввод от клиента — ожидаемый
сценарий, а не исключительный. На исключениях тесты 400-ок проверяли бы
middleware, а не правило.

**Почему брокер in-memory в интеграционных тестах.** Их предмет — HTTP и БД.
Настоящая доставка через RabbitMQ проверяется в E2E, где она и является
предметом.

**Почему заказ рождается только через `Order.Create`.** Сеттеры агрегата
приватны, конструктор закрыт: обойти валидацию и получить заказ с отрицательной
суммой невозможно по конструкции.
```

- [ ] **Step 6: Коммит**

```powershell
Set-Location C:\Users\090\Documents\GitHub\ShopLite
git add README.md .gitignore
git commit -m "docs: document test suite, coverage and design decisions"
```

---

## Критерии готовности Этапа 1

- [ ] `dotnet test ShopLite.sln` из каталога ShopLite — зелёный, 42 теста.
- [ ] Каждый эндпоинт обоих API имеет минимум один интеграционный тест: `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}`.
- [ ] Покрытие `OrderService.Domain` + `Order` — 60–70%.
- [ ] Заказ с пустыми позициями, нулевым количеством или отрицательной ценой отклоняется с 400 — подтверждено тестом.
- [ ] E2E доказывает доставку `OrderCreated` через настоящий RabbitMQ.

## Что осознанно не делается

- Outbox, идемпотентность, сага — Этап 2.
- OpenTelemetry, Serilog, health checks, метрики — Этап 3.
- Kubernetes, Helm — Этап 4.
- `ci.yml`, бейджи, GHCR — Этап 5. Там же ставится `10.0.x` через `setup-dotnet`.
- Тесты Gateway: YARP-конфиг без собственного кода, покрывать нечего до Этапа 4.
- Тесты `NotificationService` в отрыве: консьюмер только логирует; его поведение проверяется E2E.
