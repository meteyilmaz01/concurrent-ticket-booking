using Microsoft.AspNetCore.Mvc;
using TicketBooking.Application.Interfaces.Services;

namespace TicketBooking.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet]
    public async Task<IActionResult> GetEvents()
    {
        var result = await _eventService.GetEventsAsync();
        return Ok(result);
    }

    [HttpGet("{eventId}/seats")]
    public async Task<IActionResult> GetSeats(Guid eventId)
    {
        var result = await _eventService.GetSeatsAsync(eventId);
        return Ok(result);
    }
}
