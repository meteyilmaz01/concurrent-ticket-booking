using TicketBooking.Application.DTOs.PaymentDTOs;

namespace TicketBooking.Application.Interfaces.ExternalServices;

public interface IPaymentGateway
{
    // Takes the money. Throws PaymentFailedException if the provider says no.
    Task<PaymentResultDto> ChargeAsync(Guid reservationId, long amountInKurus);
}
