using TicketBooking.Domain.Enums;

namespace TicketBooking.Domain.Entities;

public class Seat
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EventId { get; set; }
    public string Section { get; set; } = string.Empty;
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public long PriceInKurus { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;

    // The reservation currently holding (or owning) the seat, and until when.
    public Guid? ReservationId { get; set; }
    public DateTime? HoldExpiresAtUtc { get; set; }

    // Chapter 1 fix: row version used for optimistic concurrency (PostgreSQL xmin).
    public uint Version { get; set; }

    public Event Event { get; set; } = null!;
}
