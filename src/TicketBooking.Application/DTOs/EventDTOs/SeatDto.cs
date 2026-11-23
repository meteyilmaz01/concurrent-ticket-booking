namespace TicketBooking.Application.DTOs.EventDTOs;

public class SeatDto
{
    public Guid Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public long PriceInKurus { get; set; }
    public string Status { get; set; } = string.Empty;
}
