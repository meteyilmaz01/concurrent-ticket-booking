using TicketBooking.Domain.Entities;

namespace TicketBooking.Application.Interfaces.Repositories;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id);
    Task AddAsync(Reservation reservation);
    Task SaveChangesAsync();
}
