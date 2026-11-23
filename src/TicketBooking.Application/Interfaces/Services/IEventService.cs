using TicketBooking.Application.DTOs.EventDTOs;

namespace TicketBooking.Application.Interfaces.Services;

public interface IEventService
{
    Task<List<EventDto>> GetEventsAsync();
    Task<List<SeatDto>> GetSeatsAsync(Guid eventId);
}
