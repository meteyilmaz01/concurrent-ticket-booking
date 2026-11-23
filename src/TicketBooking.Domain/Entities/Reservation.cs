using TicketBooking.Domain.Enums;

namespace TicketBooking.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EventId { get; set; }
    public Guid SeatId { get; set; }
    public Guid UserId { get; set; }
    public long TotalInKurus { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }

    // A pending reservation past this time counts as expired.
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public Event Event { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
}
