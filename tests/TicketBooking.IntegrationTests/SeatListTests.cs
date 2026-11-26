using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketBooking.Application.DTOs.EventDTOs;
using TicketBooking.Infrastructure.Persistence;

namespace TicketBooking.IntegrationTests;

public class SeatListTests : IClassFixture<TicketBookingApiFactory>
{
    private readonly TicketBookingApiFactory _factory;
    private readonly HttpClient _client;

    public SeatListTests(TicketBookingApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_seat_whose_hold_has_expired_is_listed_as_available()
    {
        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/events");
        var seatsUrl = $"/api/events/{events![0].Id}/seats";
        var seat = (await _client.GetFromJsonAsync<List<SeatDto>>(seatsUrl))!.First(s => s.Status == "Available");

        var response = await _client.PostAsJsonAsync("/api/reservations", new { seatId = seat.Id, userId = Guid.NewGuid() });
        response.EnsureSuccessStatusCode();

        await ExpireHoldAsync(seat.Id);

        var listed = (await _client.GetFromJsonAsync<List<SeatDto>>(seatsUrl))!.Single(s => s.Id == seat.Id);
        Assert.Equal("Available", listed.Status);
    }

    // Moves the hold into the past instead of waiting 10 minutes.
    private async Task ExpireHoldAsync(Guid seatId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Seats
            .Where(s => s.Id == seatId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.HoldExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
    }
}
