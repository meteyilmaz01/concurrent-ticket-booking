namespace TicketBooking.Application.DTOs.ReservationDTOs;

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid SeatId { get; set; }
    public Guid UserId { get; set; }
    public long TotalInKurus { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
