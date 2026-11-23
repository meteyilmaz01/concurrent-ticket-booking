# Concurrent Ticket Booking

An individual learning project: the backend of a ticket-selling API for concerts and events under heavy demand.

When thousands of people try to buy the same seat at the same moment, code that looks perfectly correct can sell one seat to many people, slow down to a crawl or be flooded with duplicate requests. In this project I write those mistakes **on purpose**, prove each one with a test and real numbers, then fix it and explain why the fix works.

Every chapter follows the same steps:

1. **Broken on purpose:** a realistic implementation that a careful engineer could write and that passes a normal code review. It is committed and tagged `chN-broken`.
2. **Proof:** a demo script, a database query and an automated test that show the problem with concrete numbers and screenshots.
3. **Fix:** the smallest change that solves it, tagged `chN-fixed`, with the same proof run again.
4. **Write-up:** a chapter document explaining the problem, the fix and the alternatives.

## Chapters

| # | Problem | Status | Write-up | Tags |
|---|---|---|---|---|
| 1 | Overselling: two buyers get the same seat | ✅ Fixed | [Chapter 1](docs/chapters/01-overselling.md) | `ch1-broken` → `ch1-fixed` |
| 2 | In-process lock that breaks with multiple API instances | ⏳ Planned | | |
| 3 | External payment call inside a database transaction | ⏳ Planned | | |
| 4 | Missing idempotency: double clicks and retries | ⏳ Planned | | |
| 5 | Seat map polling storm | ⏳ Planned | | |

### Chapter 1 at a glance

50 concurrent requests for the same seat:

| | Successful reservations for one seat | Integration test |
|---|---|---|
| Broken (`ch1-broken`) | up to **43** | ❌ fails |
| Fixed (`ch1-fixed`) | exactly **1** | ✅ passes |

Fixed with optimistic concurrency (PostgreSQL `xmin`). Details, screenshots and alternatives are in the [Chapter 1 write-up](docs/chapters/01-overselling.md).

## Tech stack

- .NET 10, ASP.NET Core Web API (controllers)
- Entity Framework Core 10 with Npgsql, migrations for the schema
- PostgreSQL 18.7 in Docker
- xUnit v3, Testcontainers (a throwaway PostgreSQL per test run), `WebApplicationFactory`

Planned for later chapters: Redis, SignalR, k6, OpenTelemetry, .NET Aspire.

## Architecture

Clean Architecture with services and repositories:

```
TicketBooking.Api             Controllers, exception handling middleware, Program.cs
        │
        ▼
TicketBooking.Application     Services (business rules), DTOs, exceptions,
        │                     repository and service interfaces
        ▼
TicketBooking.Domain          Entities (Event, Seat, Reservation) and enums

TicketBooking.Infrastructure  EF Core DbContext, configurations, migrations,
                              repository implementations (references Application and Domain)
```

A request flows as **Controller → Service → Repository → DbContext**.

```
src/
├── TicketBooking.Api/
├── TicketBooking.Application/
├── TicketBooking.Domain/
└── TicketBooking.Infrastructure/
tests/
└── TicketBooking.IntegrationTests/
scripts/
└── oversell-demo.cs
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

In the Development environment the API applies migrations and creates a demo event with 100 seats on startup. It listens on `http://localhost:5287`.

| Endpoint | Description |
|---|---|
| `GET /api/events` | List events |
| `GET /api/events/{eventId}/seats` | List the seats of an event and their status |
| `POST /api/reservations` | Reserve a seat. Body: `{ "seatId": "...", "userId": "..." }` |

### 4. Run the tests

Docker must be running: the tests start their own PostgreSQL container and never touch the development database.

```bash
dotnet test --project tests/TicketBooking.IntegrationTests
```

### 5. Run the demo script

With the API running, in a second terminal:

```bash
dotnet run scripts/oversell-demo.cs
```

It sends 50 reservation requests for the same seat at the same time and reports how many succeeded.

## Conventions

- Ids are UUID v7 (`Guid.CreateVersion7()`).
- Money is stored in kuruş as `long`; no `decimal`.
- Times are UTC `DateTime`, stored as `timestamptz`.
- Code that is wrong on purpose is marked with a comment that names its chapter.
