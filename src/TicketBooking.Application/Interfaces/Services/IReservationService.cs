using TicketBooking.Application.DTOs.ReservationDTOs;

namespace TicketBooking.Application.Interfaces.Services;

public interface IReservationService
{
    Task<ReservationDto> ReserveSeatAsync(ReserveSeatRequestDto request);
}
