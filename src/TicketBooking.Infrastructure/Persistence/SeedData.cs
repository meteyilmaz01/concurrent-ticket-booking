using Microsoft.EntityFrameworkCore;
using TicketBooking.Domain.Entities;

namespace TicketBooking.Infrastructure.Persistence;

public static class SeedData
{
    // Creates one demo event with 100 seats (section A, rows 1-10, seats 1-10) if the database is empty.
    public static async Task AddDemoEventAsync(AppDbContext db)
    {
        if (await db.Events.AnyAsync())
        {
            return;
        }

        var demoEvent = new Event
        {
            Name = "Demo Concert",
            StartsAtUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow,
        };

        for (var row = 1; row <= 10; row++)
        {
            for (var number = 1; number <= 10; number++)
            {
                demoEvent.Seats.Add(new Seat
                {
                    Section = "A",
                    Row = row.ToString(),
                    Number = number,
                    PriceInKurus = 150_000, // 1,500.00 TRY
                });
            }
        }

        db.Events.Add(demoEvent);
        await db.SaveChangesAsync();
    }

    // Chapter 2+: a large event for load tests, with 10,000 seats (section B, rows 1-100, seats 1-100).
    // It starts later than the demo event, so the demo event stays first in the event list.
    public const string LoadTestEventName = "Load Test Arena";

    public static async Task AddLoadTestEventAsync(AppDbContext db)
    {
        if (await db.Events.AnyAsync(e => e.Name == LoadTestEventName))
        {
            return;
        }

        var loadTestEvent = new Event
        {
            Name = LoadTestEventName,
            StartsAtUtc = DateTime.UtcNow.AddDays(60),
            CreatedAtUtc = DateTime.UtcNow,
        };

        for (var row = 1; row <= 100; row++)
        {
            for (var number = 1; number <= 100; number++)
            {
                loadTestEvent.Seats.Add(new Seat
                {
                    Section = "B",
                    Row = row.ToString(),
                    Number = number,
                    PriceInKurus = 75_000, // 750.00 TRY
                });
            }
        }

        db.Events.Add(loadTestEvent);
        await db.SaveChangesAsync();
    }
}
