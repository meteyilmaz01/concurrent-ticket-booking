namespace TicketBooking.Application.DTOs.EventDTOs;

public class EventDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
}
