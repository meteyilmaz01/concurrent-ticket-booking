using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TicketBooking.Application.DTOs.EventDTOs;
using TicketBooking.Application.DTOs.PaymentDTOs;
using TicketBooking.Application.DTOs.ReservationDTOs;
using TicketBooking.Application.Exceptions;
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

    [Fact]
    public async Task Confirming_a_paid_reservation_again_returns_the_same_payment_without_charging()
    {
        var reservation = await ReserveSeatAsync();

        var first = await ConfirmAsync(reservation.Id);
        var second = await ConfirmAsync(reservation.Id);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<ReservationDto>())!.PaymentId,
            (await second.Content.ReadFromJsonAsync<ReservationDto>())!.PaymentId);
        Assert.Equal(1, _gateway.ChargeCount);
    }

    [Fact]
    public async Task A_declined_payment_can_be_tried_again()
    {
        var reservation = await ReserveSeatAsync();
        _gateway.DeclineNext = true;

        var declined = await ConfirmAsync(reservation.Id);
        var retry = await ConfirmAsync(reservation.Id);

        Assert.Equal(HttpStatusCode.BadGateway, declined.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    private Task<HttpResponseMessage> ConfirmAsync(Guid reservationId)
    {
        return _client.PostAsync($"/api/reservations/{reservationId}/confirm", null);
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

    // Stands in for the payment provider: takes a moment, counts the calls, can decline once.
    private class CountingPaymentGateway : IPaymentGateway
    {
        private int _chargeCount;

        public int ChargeCount => _chargeCount;

        public bool DeclineNext { get; set; }

        public async Task<PaymentResultDto> ChargeAsync(Guid reservationId, long amountInKurus)
        {
            Interlocked.Increment(ref _chargeCount);
            await Task.Delay(500);

            if (DeclineNext)
            {
                DeclineNext = false;
                throw new PaymentFailedException("Declined.");
            }

            return new PaymentResultDto { PaymentId = Guid.CreateVersion7() };
        }
    }
}
