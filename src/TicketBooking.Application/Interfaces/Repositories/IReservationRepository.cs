using TicketBooking.Domain.Entities;

namespace TicketBooking.Application.Interfaces.Repositories;

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation);
    Task SaveChangesAsync();
}
