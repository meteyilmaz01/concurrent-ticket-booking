using TicketBooking.Domain.Entities;

namespace TicketBooking.Application.Interfaces.Repositories;

public interface IPaymentRepository
{
    // Saves immediately. Returns false if the reservation already has a payment.
    Task<bool> TryAddAsync(Payment payment);

    // Saves immediately.
    Task RemoveAsync(Payment payment);
}
