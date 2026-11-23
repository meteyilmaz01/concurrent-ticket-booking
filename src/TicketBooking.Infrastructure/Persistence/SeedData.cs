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
}
