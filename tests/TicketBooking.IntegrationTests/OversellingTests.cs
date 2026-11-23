using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketBooking.Application.DTOs.EventDTOs;
using TicketBooking.Infrastructure.Persistence;

namespace TicketBooking.IntegrationTests;

// Chapter 1: proves that one seat can be sold to more than one person.
public class OversellingTests : IClassFixture<TicketBookingApiFactory>
{
    private const int RequestsPerSeat = 50;
    private const int SeatsToTry = 5;

    private readonly TicketBookingApiFactory _factory;
    private readonly HttpClient _client;

    public OversellingTests(TicketBookingApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_seat_create_only_one_reservation()
    {
        var seats = await GetAvailableSeatsAsync(SeatsToTry);
        var results = new List<string>();
        var oversold = false;

        foreach (var seat in seats)
        {
            var successfulResponses = await ReserveConcurrentlyAsync(seat.Id, RequestsPerSeat);
            var reservationsInDatabase = await CountReservationsInDatabaseAsync(seat.Id);

            results.Add($"Seat {seat.Section}-{seat.Row}-{seat.Number}: " +
                        $"{successfulResponses} successful responses, {reservationsInDatabase} reservations in database");

            if (successfulResponses != 1 || reservationsInDatabase != 1)
            {
                oversold = true;
            }
        }

        Assert.False(oversold, "Each seat must be reserved exactly once.\n" + string.Join("\n", results));
    }

    // Sends many requests for one seat, all released at the same moment.
    private async Task<int> ReserveConcurrentlyAsync(Guid seatId, int requestCount)
    {
        var startSignal = new TaskCompletionSource();
        var tasks = new List<Task<HttpResponseMessage>>();

        for (var i = 0; i < requestCount; i++)
        {
            var body = new { seatId, userId = Guid.NewGuid() };

            tasks.Add(Task.Run(async () =>
            {
                await startSignal.Task; // wait for the start signal
                return await _client.PostAsJsonAsync("/api/reservations", body);
            }));
        }

        startSignal.SetResult(); // release all requests together
        var responses = await Task.WhenAll(tasks);

        return responses.Count(r => r.StatusCode == HttpStatusCode.OK);
    }

    private async Task<List<SeatDto>> GetAvailableSeatsAsync(int count)
    {
        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/events");
        var seats = await _client.GetFromJsonAsync<List<SeatDto>>($"/api/events/{events![0].Id}/seats");

        return seats!.Where(s => s.Status == "Available").Take(count).ToList();
    }

    private async Task<int> CountReservationsInDatabaseAsync(Guid seatId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Reservations.CountAsync(r => r.SeatId == seatId);
    }
}
