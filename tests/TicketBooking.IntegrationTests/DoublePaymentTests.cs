using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TicketBooking.Application.DTOs.EventDTOs;
using TicketBooking.Application.DTOs.PaymentDTOs;
using TicketBooking.Application.DTOs.ReservationDTOs;
using TicketBooking.Application.Interfaces.ExternalServices;

namespace TicketBooking.IntegrationTests;

// Chapter 3: proves that one reservation can be charged more than once.
public class DoublePaymentTests : IClassFixture<TicketBookingApiFactory>
{
    private const int ParallelConfirms = 10;

    private readonly CountingPaymentGateway _gateway = new();
    private readonly HttpClient _client;

    public DoublePaymentTests(TicketBookingApiFactory factory)
    {
        // Same API and database, but the payment provider is replaced so we can count the charges.
        _client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IPaymentGateway>(_gateway)))
            .CreateClient();
    }

    [Fact]
    public async Task Parallel_confirms_of_one_reservation_charge_the_customer_only_once()
    {
        var reservation = await ReserveSeatAsync();
        var startSignal = new TaskCompletionSource();
        var tasks = new List<Task<HttpResponseMessage>>();

        for (var i = 0; i < ParallelConfirms; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await startSignal.Task; // wait for the start signal
                return await _client.PostAsync($"/api/reservations/{reservation.Id}/confirm", null);
            }));
        }

        startSignal.SetResult(); // release all requests together
        var responses = await Task.WhenAll(tasks);
        var successful = responses.Count(r => r.StatusCode == HttpStatusCode.OK);

        Assert.True(
            _gateway.ChargeCount == 1,
            $"The customer must be charged exactly once.\n" +
            $"{ParallelConfirms} confirm requests: {_gateway.ChargeCount} charges, {successful} successful responses.");
    }

    private async Task<ReservationDto> ReserveSeatAsync()
    {
        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/events");
        var seats = await _client.GetFromJsonAsync<List<SeatDto>>($"/api/events/{events![0].Id}/seats");
        var seat = seats!.First(s => s.Status == "Available");

        var response = await _client.PostAsJsonAsync("/api/reservations", new { seatId = seat.Id, userId = Guid.NewGuid() });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ReservationDto>())!;
    }

    // Stands in for the payment provider: takes a moment, always succeeds, counts the calls.
    private class CountingPaymentGateway : IPaymentGateway
    {
        private int _chargeCount;

        public int ChargeCount => _chargeCount;

        public async Task<PaymentResultDto> ChargeAsync(Guid reservationId, long amountInKurus)
        {
            Interlocked.Increment(ref _chargeCount);
            await Task.Delay(500);

            return new PaymentResultDto { PaymentId = Guid.CreateVersion7() };
        }
    }
}
