using System.Net.Http.Json;
using TicketBooking.Application.DTOs.PaymentDTOs;
using TicketBooking.Application.Exceptions;
using TicketBooking.Application.Interfaces.ExternalServices;

namespace TicketBooking.Infrastructure.ExternalServices;

// Calls the payment provider over HTTP (in this project: TicketBooking.FakePaymentGateway).
public class HttpPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;

    public HttpPaymentGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentResultDto> ChargeAsync(Guid reservationId, long amountInKurus)
    {
        var response = await _httpClient.PostAsJsonAsync("/payments", new { reservationId, amountInKurus });

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentFailedException("Payment was declined or the payment provider is unavailable.");
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        return new PaymentResultDto { PaymentId = payment!.Id };
    }

    private class PaymentResponse
    {
        public Guid Id { get; set; }
    }
}
