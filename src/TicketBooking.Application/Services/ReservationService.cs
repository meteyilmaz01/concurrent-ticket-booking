using TicketBooking.Application.DTOs.ReservationDTOs;
using TicketBooking.Application.Exceptions;
using TicketBooking.Application.Interfaces.ExternalServices;
using TicketBooking.Application.Interfaces.Repositories;
using TicketBooking.Application.Interfaces.Services;
using TicketBooking.Domain.Entities;
using TicketBooking.Domain.Enums;

namespace TicketBooking.Application.Services;

public class ReservationService : IReservationService
{
    private readonly ISeatRepository _seatRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IPaymentGateway _paymentGateway;

    public ReservationService(
        ISeatRepository seatRepository,
        IReservationRepository reservationRepository,
        IPaymentGateway paymentGateway)
    {
        _seatRepository = seatRepository;
        _reservationRepository = reservationRepository;
        _paymentGateway = paymentGateway;
    }

    // Chapter 1: Overselling
    //
    // WAS INTENTIONALLY BROKEN (tag ch1-broken):
    // Check-then-act race: the seat is read, checked in memory, then written back.
    // Two concurrent requests can both read "Available" before either one saves,
    // so both pass the check and both get a reservation for the same seat.
    // Nothing in the database stopped this: no lock, no concurrency token,
    // no constraint on active reservations per seat.
    //
    // FIXED (tag ch1-fixed) with optimistic concurrency, without changing this method:
    // - Seat.Version is mapped to PostgreSQL's xmin (SeatConfiguration).
    // - The UPDATE in step 3 only succeeds if the seat is unchanged since step 1.
    // - The losing request gets DbUpdateConcurrencyException, which
    //   ReservationRepository turns into ConflictException (409). Nothing is saved for it.
    //
    // Full write-up: docs/chapters/01-overselling.md
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

        return ToDto(reservation);
    }

    // Chapter 2: Payment inside a database transaction
    //
    // WAS INTENTIONALLY BROKEN (tag ch2-broken):
    // The whole confirmation ran in one transaction so that "if the payment fails,
    // nothing is saved". But a transaction holds a database connection from Begin
    // until Commit, and the payment provider takes ~2 seconds to answer. Under load
    // the connection pool ran out, and unrelated requests (listing events) had to
    // wait for a free connection.
    //
    // FIXED (tag ch2-fixed): no transaction around the payment call.
    //
    // STILL OPEN (Chapter 3):
    // - Two parallel confirms of the same reservation can both charge the customer.
    //   xmin rejects the second save, but the money has already been taken.
    // - If the save fails after a successful charge (for example the hold expired and
    //   someone else took the seat), the customer is charged without getting the seat.
    //
    // Full write-up: docs/chapters/02-payment-inside-transaction.md
    public async Task<ReservationDto> ConfirmAsync(Guid reservationId)
    {
        var now = DateTime.UtcNow;

        // 1) Read the reservation and its seat.
        // Without an explicit transaction, EF Core returns the connection to the pool
        // after each query.
        var reservation = await _reservationRepository.GetByIdAsync(reservationId);
        if (reservation == null)
        {
            throw new NotFoundException("Reservation not found.");
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new ConflictException("Reservation is not pending.");
        }

        if (reservation.ExpiresAtUtc <= now)
        {
            throw new ConflictException("Reservation has expired.");
        }

        var seat = await _seatRepository.GetByIdAsync(reservation.SeatId);
        if (seat == null || seat.ReservationId != reservation.Id)
        {
            throw new ConflictException("Seat is no longer held by this reservation.");
        }

        // 2) Take the money. No database connection is held while we wait.
        var payment = await _paymentGateway.ChargeAsync(reservation.Id, reservation.TotalInKurus);

        // 3) Mark the reservation confirmed and the seat sold.
        // One SaveChanges is atomic by itself, so both rows change together or not at all.
        reservation.Status = ReservationStatus.Confirmed;
        reservation.ConfirmedAtUtc = now;
        reservation.PaymentId = payment.PaymentId;

        seat.Status = SeatStatus.Sold;
        seat.HoldExpiresAtUtc = null;

        await _reservationRepository.SaveChangesAsync();

        return ToDto(reservation);
    }

    private static ReservationDto ToDto(Reservation reservation)
    {
        return new ReservationDto
        {
            Id = reservation.Id,
            SeatId = reservation.SeatId,
            UserId = reservation.UserId,
            TotalInKurus = reservation.TotalInKurus,
            Status = reservation.Status.ToString(),
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            PaymentId = reservation.PaymentId,
        };
    }
}
