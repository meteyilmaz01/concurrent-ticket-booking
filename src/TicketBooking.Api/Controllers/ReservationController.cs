using Microsoft.AspNetCore.Mvc;
using TicketBooking.Application.DTOs.ReservationDTOs;
using TicketBooking.Application.Interfaces.Services;

namespace TicketBooking.Api.Controllers;

[ApiController]
[Route("api/reservations")]
public class ReservationController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost]
    public async Task<IActionResult> ReserveSeat([FromBody] ReserveSeatRequestDto request)
    {
        var result = await _reservationService.ReserveSeatAsync(request);
        return Ok(result);
    }
}
