namespace TicketBooking.Domain.Entities;

// Chapter 3 fix: written BEFORE the money is taken. A unique constraint on
// ReservationId lets only one request per reservation call the payment provider.
public class Payment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ReservationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Reservation Reservation { get; set; } = null!;
}
