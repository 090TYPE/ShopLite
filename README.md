# ShopLite — .NET 9 Microservices

A minimal but production-shaped microservices project built with .NET 9.  
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
| ORM | EF Core 9 + PostgreSQL |
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
├── NotificationService/        # Worker: consumes OrderCreated
├── Gateway/                    # YARP reverse proxy
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
