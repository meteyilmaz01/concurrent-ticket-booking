# Concurrent Ticket Booking

[![CI](https://github.com/meteyilmaz01/concurrent-ticket-booking/actions/workflows/ci.yml/badge.svg)](https://github.com/meteyilmaz01/concurrent-ticket-booking/actions/workflows/ci.yml)

An individual learning project: the backend of a ticket-selling API for concerts and events under heavy demand.

When thousands of people try to buy the same seat at the same moment, code that looks perfectly correct can sell one seat to many people, slow down to a crawl or be flooded with duplicate requests. In this project I write those mistakes **on purpose**, prove each one with a test and real numbers, then fix it and explain why the fix works.

Every chapter follows the same steps:

1. **Broken on purpose:** a realistic implementation that a careful engineer could write and that passes a normal code review. It is committed and tagged `chN-broken`.
2. **Proof:** a test (integration test or load test) that shows the problem with concrete numbers and screenshots.
3. **Fix:** the smallest change that solves it, tagged `chN-fixed`, with the same proof run again.
4. **Write-up:** a chapter document explaining the problem, the fix and the alternatives.

## Chapters

| # | Problem | Status | Write-up | Tags |
|---|---|---|---|---|
| 1 | Overselling: two buyers get the same seat | ✅ Fixed | [Chapter 1](docs/chapters/01-overselling.md) | `ch1-broken` → `ch1-fixed` |
| 2 | External payment call inside a database transaction | ✅ Fixed | [Chapter 2](docs/chapters/02-payment-inside-transaction.md) | `ch2-broken` → `ch2-fixed` |
| 3 | Duplicate payment: double clicks and retries charge again | ✅ Fixed | [Chapter 3](docs/chapters/03-duplicate-payment.md) | `ch3-broken` → `ch3-fixed` |

### Chapter 1 at a glance

50 concurrent requests for the same seat:

| | Successful reservations for one seat | Integration test |
|---|---|---|
| Broken (`ch1-broken`) | up to **43** | ❌ fails |
| Fixed (`ch1-fixed`) | exactly **1** | ✅ passes |

Fixed with optimistic concurrency (PostgreSQL `xmin`). Details, screenshots and alternatives are in the [Chapter 1 write-up](docs/chapters/01-overselling.md).

### Chapter 2 at a glance

200 users paying at the same time, while others only list events (p95 response times):

| | Event list | Payment |
|---|---|---|
| Broken (`ch2-broken`) | 2.08 s | 3.92 s |
| Fixed (`ch2-fixed`) | **42 ms** | **2.50 s** (the provider alone takes 2 s) |

Fixed by not holding a database transaction while calling the payment provider. Details are in the [Chapter 2 write-up](docs/chapters/02-payment-inside-transaction.md).

### Chapter 3 at a glance

10 concurrent payment requests for one reservation of 1,500 TRY:

| | Charges | Money taken |
|---|---|---|
| Broken (`ch3-broken`) | **10** | 15,000 TRY |
| Fixed (`ch3-fixed`) | **1** | 1,500 TRY |

Fixed by saving a payment row with a unique constraint before charging. Details are in the [Chapter 3 write-up](docs/chapters/03-duplicate-payment.md).

## Out of scope

This project is about specific concurrency and performance problems, so these are deliberately left out:

- **Authentication:** `userId` is taken from the request body. A real system must take it from a verified token.
- **Real payments:** a fake payment gateway stands in for a provider.
- **Refunds and reconciliation** for charges whose outcome is unknown or could not be saved.
- **Production setup:** deployment, secrets management and connection limits are not tuned.

## Tech stack

- .NET 10, ASP.NET Core Web API (controllers)
- Entity Framework Core 10 with Npgsql, migrations for the schema
- PostgreSQL 18.7 in Docker
- xUnit v3, Testcontainers (a throwaway PostgreSQL per test run), `WebApplicationFactory`
- k6 (run from Docker) for load tests

## Architecture

Clean Architecture with services and repositories:

```
TicketBooking.Api             Controllers, exception handling middleware, Program.cs
        │
        ▼
TicketBooking.Application     Services (business rules), DTOs, exceptions,
        │                     repository and service interfaces
        ▼
TicketBooking.Domain          Entities (Event, Seat, Reservation, Payment) and enums

TicketBooking.Infrastructure  EF Core DbContext, configurations, migrations,
                              repository implementations, payment gateway client
                              (references Application and Domain)

TicketBooking.FakePaymentGateway   Separate service that stands in for a payment provider
```

A request flows as **Controller → Service → Repository → DbContext**.

```
src/
├── TicketBooking.Api/
├── TicketBooking.Application/
├── TicketBooking.Domain/
├── TicketBooking.FakePaymentGateway/
└── TicketBooking.Infrastructure/
tests/
└── TicketBooking.IntegrationTests/
load-tests/
└── payment-load.js
scripts/
├── oversell-demo.cs
└── double-charge-demo.cs
docs/
├── chapters/          one write-up per chapter
└── images/            screenshots used as proof
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running)

### 1. Start the database

Copy `.env.example` to `.env` and choose a password for `POSTGRES_PASSWORD`. Then:

```bash
docker compose up -d
```

### 2. Configure the connection string

The API reads its connection string from user secrets, so the password never ends up in the repository. Use the same password as in `.env`:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=ticketbooking;Username=ticketbooking;Password=<your password>" --project src/TicketBooking.Api
```

### 3. Run the API

```bash
dotnet run --project src/TicketBooking.Api
```

In the Development environment the API applies migrations on startup and creates a demo event with 100 seats and a load test event with 10,000 seats. It listens on `http://localhost:5287`.

Paying for a reservation also needs the fake payment gateway, in a second terminal (listens on `http://localhost:5288`):

```bash
dotnet run --project src/TicketBooking.FakePaymentGateway
```

| Endpoint | Description |
|---|---|
| `GET /api/events` | List events |
| `GET /api/events/{eventId}/seats` | List the seats of an event and their status |
| `POST /api/reservations` | Reserve a seat. Body: `{ "seatId": "...", "userId": "..." }` |
| `POST /api/reservations/{reservationId}/confirm` | Pay for a reservation |

### 4. Run the tests

Docker must be running: the tests start their own PostgreSQL container and never touch the development database.

```bash
dotnet test --project tests/TicketBooking.IntegrationTests
```

### 5. Run the demo scripts

With the API running, in a second terminal:

```bash
dotnet run scripts/oversell-demo.cs
```

It sends 50 reservation requests for the same seat at the same time and reports how many succeeded.

With the fake payment gateway running as well:

```bash
dotnet run scripts/double-charge-demo.cs
```

It pays one reservation 10 times at once and reports how many times the money was taken.

### 6. Run the load test

With the API and the fake payment gateway running:

```bash
docker compose run --rm k6 run /scripts/payment-load.js
```

See [Chapter 2](docs/chapters/02-payment-inside-transaction.md) for what it measures and how to reset the data.

## Conventions

- Ids are UUID v7 (`Guid.CreateVersion7()`).
- Money is stored in kuruş as `long`; no `decimal`.
- Times are UTC `DateTime`, stored as `timestamptz`.
- Code that is wrong on purpose is marked with a comment that names its chapter.
