using TicketBooking.Application.DTOs.EventDTOs;
using TicketBooking.Application.Exceptions;
using TicketBooking.Application.Interfaces.Repositories;
using TicketBooking.Application.Interfaces.Services;
using TicketBooking.Domain.Enums;

namespace TicketBooking.Application.Services;

public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly ISeatRepository _seatRepository;

    public EventService(IEventRepository eventRepository, ISeatRepository seatRepository)
    {
        _eventRepository = eventRepository;
        _seatRepository = seatRepository;
    }

    public async Task<List<EventDto>> GetEventsAsync()
    {
        var events = await _eventRepository.GetAllAsync();

        return events.Select(e => new EventDto
        {
            Id = e.Id,
            Name = e.Name,
            StartsAtUtc = e.StartsAtUtc,
        }).ToList();
    }

    public async Task<List<SeatDto>> GetSeatsAsync(Guid eventId)
    {
        var ev = await _eventRepository.GetByIdAsync(eventId);
        if (ev == null)
        {
            throw new NotFoundException("Event not found.");
        }

        var seats = await _seatRepository.GetByEventIdAsync(eventId);

        var now = DateTime.UtcNow;

        return seats.Select(s => new SeatDto
        {
            Id = s.Id,
            Section = s.Section,
            Row = s.Row,
            Number = s.Number,
            PriceInKurus = s.PriceInKurus,
            // Expired holds are not cleaned up in the database; they count as free,
            // the same rule ReservationService applies when reserving.
            Status = s.Status == SeatStatus.Held && s.HoldExpiresAtUtc <= now
                ? SeatStatus.Available.ToString()
                : s.Status.ToString(),
        }).ToList();
    }
}
