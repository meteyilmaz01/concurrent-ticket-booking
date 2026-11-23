namespace TicketBooking.Application.DTOs.ReservationDTOs;

public class ReserveSeatRequestDto
{
    public Guid SeatId { get; set; }
    public Guid UserId { get; set; }
}
