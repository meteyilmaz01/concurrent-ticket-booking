using TicketBooking.Application.DTOs.ReservationDTOs;
using TicketBooking.Application.Exceptions;
using TicketBooking.Application.Interfaces.Repositories;
using TicketBooking.Application.Interfaces.Services;
using TicketBooking.Domain.Entities;
using TicketBooking.Domain.Enums;

namespace TicketBooking.Application.Services;

public class ReservationService : IReservationService
{
    private readonly ISeatRepository _seatRepository;
    private readonly IReservationRepository _reservationRepository;

    public ReservationService(ISeatRepository seatRepository, IReservationRepository reservationRepository)
    {
        _seatRepository = seatRepository;
        _reservationRepository = reservationRepository;
    }

    // INTENTIONALLY BROKEN — Chapter 1: Overselling
    // Check-then-act race: the seat is read, checked in memory, then written back.
    // Two concurrent requests can both read "Available" before either one saves,
    // so both pass the check and both get a reservation for the same seat.
    // Nothing in the database stops this: no lock, no concurrency token,
    // no constraint on active reservations per seat.
    public async Task<ReservationDto> ReserveSeatAsync(ReserveSeatRequestDto request)
    {
        var now = DateTime.UtcNow;

        // 1) Read the seat
        var seat = await _seatRepository.GetByIdAsync(request.SeatId);
        if (seat == null)
        {
            throw new NotFoundException("Seat not found.");
        }

        // 2) Check it is free (a hold that has expired counts as free)
        var isFree = seat.Status == SeatStatus.Available
            || (seat.Status == SeatStatus.Held && seat.HoldExpiresAtUtc <= now);

        if (!isFree)
        {
            throw new ConflictException("Seat is not available.");
        }

        // 3) Write: hold the seat for 10 minutes and create the reservation
        var reservation = new Reservation
        {
            EventId = seat.EventId,
            SeatId = seat.Id,
            UserId = request.UserId,
            TotalInKurus = seat.PriceInKurus,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(10),
        };

        seat.Status = SeatStatus.Held;
        seat.ReservationId = reservation.Id;
        seat.HoldExpiresAtUtc = reservation.ExpiresAtUtc;

        await _reservationRepository.AddAsync(reservation);
        await _reservationRepository.SaveChangesAsync();

        return new ReservationDto
        {
            Id = reservation.Id,
            SeatId = reservation.SeatId,
            UserId = reservation.UserId,
            TotalInKurus = reservation.TotalInKurus,
            Status = reservation.Status.ToString(),
            ExpiresAtUtc = reservation.ExpiresAtUtc,
        };
    }
}
