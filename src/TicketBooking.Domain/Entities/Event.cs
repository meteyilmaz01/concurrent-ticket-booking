namespace TicketBooking.Domain.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<Seat> Seats { get; set; } = [];
}
