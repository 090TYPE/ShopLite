# ShopLite — .NET 10 Microservices

A minimal but production-shaped microservices project built with .NET 10.  
Demonstrates async event-driven communication, isolated databases per service, and API Gateway routing.

## Architecture

```
                        ┌─────────────────────────────────────────────┐
                        │              Docker Compose                   │
                        │                                               │
  Client                │  ┌──────────┐    ┌──────────────┐            │
  (curl / Postman)  ────┼─►│ Gateway  │───►│ UserService  │◄──user-db  │
                        │  │  :5000   │    │    :5001     │            │
                        │  │  (YARP)  │    └──────────────┘            │
                        │  │          │                                 │
                        │  │          │    ┌──────────────┐            │
                        │  │          │───►│ OrderService │◄──order-db │
                        │  └──────────┘    │    :5002     │            │
                        │                  └──────┬───────┘            │
                        │                         │ publish             │
                        │                         ▼                    │
                        │                   ┌──────────┐               │
                        │                   │ RabbitMQ │               │
                        │                   └────┬─────┘               │
                        │                        │ consume              │
                        │                        ▼                     │
                        │             ┌─────────────────────┐          │
                        │             │ NotificationService  │          │
                        │             │  (Worker, no HTTP)   │          │
                        │             └─────────────────────┘          │
                        └─────────────────────────────────────────────┘
```

**Key principle**: services do NOT call each other over HTTP.  
OrderService publishes an event to the message bus — NotificationService consumes it asynchronously.

## Tech Stack

| Component | Technology |
|-----------|-----------|
| API Gateway | YARP 2.3 |
| Message Bus | MassTransit 8 + RabbitMQ |
| ORM | EF Core 10 + PostgreSQL |
| Auth | JWT Bearer |
| Containers | Docker Compose |

## Services

| Service | Responsibility | Port |
|---------|---------------|------|
| **UserService** | Registration, login, JWT issuance | 5001 |
| **OrderService** | Order creation, history, publishes `OrderCreated` event | 5002 |
| **NotificationService** | Subscribes to `OrderCreated`, sends confirmation (Worker, no HTTP) | — |
| **Gateway** | YARP reverse proxy, single entry point | 5000 |

## Quick Start

**Prerequisites:** Docker Desktop

```bash
git clone https://github.com/090TYPE/ShopLite
cd ShopLite
docker compose up --build
```

First run takes ~3 minutes (pulling .NET images + NuGet restore).

## API

All requests go through the Gateway on port **5000**.

### Register
```http
POST http://localhost:5000/api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!",
  "name": "Your Name"
}
```

### Login → get JWT
```http
POST http://localhost:5000/api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!"
}
```

### Create Order
```http
POST http://localhost:5000/api/orders
Authorization: Bearer <token>
Content-Type: application/json

{
  "items": [
    { "productName": "Laptop", "quantity": 1, "price": 999.99 },
    { "productName": "Mouse",  "quantity": 2, "price":  29.99 }
  ]
}
```

### My Orders
```http
GET http://localhost:5000/api/orders
Authorization: Bearer <token>
```

## Observing Async Communication

After creating an order, watch both containers:

```bash
docker compose logs -f order-service notification-service
```

You will see the event travel from OrderService to NotificationService:
```
shoplite-order-service         | info: ... Published OrderCreated ...
shoplite-notification-service  | 📧 [NOTIFICATION] Order #abc received — Total: $1,059.97
```

## Tests

```bash
dotnet test ShopLite.sln
```

**42 tests, no manual setup.** PostgreSQL and RabbitMQ are started as throwaway
containers by [Testcontainers](https://testcontainers.com/) — you need Docker
running, nothing else installed.

| Project | What it proves |
|---------|----------------|
| `OrderService.UnitTests` | Order invariants and status transitions, no infrastructure |
| `UserService.UnitTests` | JWT issuance: claims, expiry, signing algorithm |
| `OrderService.IntegrationTests` | Order API against a real PostgreSQL |
| `UserService.IntegrationTests` | Auth API against a real PostgreSQL |
| `ShopLite.E2ETests` | register → login → order → event delivered through a real RabbitMQ |

The E2E test is the one worth reading. It boots both services, registers a user,
takes the JWT that **UserService actually issued**, authenticates with it against
**OrderService**, places an order, and waits for `OrderCreated` to arrive at a
consumer — through a real broker in a real container. Every seam is real.

### Coverage

```bash
dotnet test ShopLite.sln --collect:"XPlat Code Coverage"
reportgenerator -reports:"tests/**/TestResults/**/coverage.cobertura.xml" \
                -targetdir:"coverage-report" -reporttypes:Html
```

Line coverage **96.6%**, branch **91.6%**, across UserService, OrderService and
Contracts. The domain — `Result<T>`, `OrderErrors`, `NewOrderItem` — is at 100%,
and `Order` at 98.1%.

Not covered, deliberately: **Gateway** is YARP configuration with no code of its
own, and **NotificationService**'s consumer only writes a log line. The E2E test
proves the event reaches *a* consumer through a real broker — it does not exercise
`OrderCreatedConsumer` itself, which sits at zero coverage until it does something
worth asserting.

## Design decisions

**Why `Result<T>` and not exceptions.** Invalid client input is an expected
outcome, not an exceptional one. With exceptions, a test for HTTP 400 ends up
verifying exception middleware rather than the business rule that rejected the
payload.

**Why an order can only be born in `Order.Create`.** `Order`'s constructor and
setters are private, and `Items` is an `IReadOnlyList` over a private field. It is
not *convention* that stops you creating an order with a negative total — it is the
compiler. Before this, `OrdersController` computed the total inline with no
validation at all, so an order with no items or a price of `-5` was accepted and
persisted. Extracting the aggregate broke the controller's compilation on the spot,
which is exactly the point.

**Why the broker is in-memory in integration tests but real in E2E.** Integration
tests exist to check HTTP and persistence; booting a broker for them buys latency,
not confidence. Delivery through RabbitMQ is a distinct claim, so it gets a distinct
test where the broker is real.

**Why services don't call each other over HTTP.** OrderService publishes an event
and does not know or care who consumes it. NotificationService can be down, slow, or
replaced without OrderService changing.

## Swagger UI

- UserService: http://localhost:5001/swagger
- OrderService: http://localhost:5002/swagger
- RabbitMQ Management: http://localhost:15672 (guest / guest)

## Project Structure

```
ShopLite/
├── Contracts/                  # Shared event contracts (OrderCreated)
├── UserService/                # JWT auth service
├── OrderService/               # Orders + event publishing
│   └── Domain/                 # Order aggregate, Result<T>, error catalogue
├── NotificationService/        # Worker: consumes OrderCreated
├── Gateway/                    # YARP reverse proxy
├── tests/
│   ├── OrderService.UnitTests/         # Domain rules, no infrastructure
│   ├── UserService.UnitTests/          # JWT issuance
│   ├── OrderService.IntegrationTests/  # Order API + real PostgreSQL
│   ├── UserService.IntegrationTests/   # Auth API + real PostgreSQL
│   └── ShopLite.E2ETests/              # Full flow through a real RabbitMQ
├── docker-compose.yml
└── ShopLite.sln
```

## Stop

```bash
# Stop containers
docker compose down

# Stop and remove databases
docker compose down -v
```
