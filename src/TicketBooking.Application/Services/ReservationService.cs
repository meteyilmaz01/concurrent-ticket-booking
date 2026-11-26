using TicketBooking.Application.DTOs.PaymentDTOs;
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
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGateway _paymentGateway;

    public ReservationService(
        ISeatRepository seatRepository,
        IReservationRepository reservationRepository,
        IPaymentRepository paymentRepository,
        IPaymentGateway paymentGateway)
    {
        _seatRepository = seatRepository;
        _reservationRepository = reservationRepository;
        _paymentRepository = paymentRepository;
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
    // Full write-up: docs/chapters/02-payment-inside-transaction.md
    //
    // Chapter 3: Duplicate payment
    //
    // WAS INTENTIONALLY BROKEN (tag ch3-broken):
    // Check-then-act again, but this time the "act" cannot be undone. A double click or
    // a client retry sent a second confirm while the first was still waiting for the
    // provider. Both saw "Pending" and both charged. Only one save succeeded, so the
    // customer paid several times and was told that the other attempts failed.
    //
    // FIXED (tag ch3-fixed): a payment row with a unique ReservationId is saved before
    // the charge, and a confirm of an already paid reservation returns the same result.
    //
    // STILL OPEN (outside the scope of this project, needs reconciliation):
    // - If the outcome of a charge is unknown (timeout, crash), the claim stays and the
    //   reservation cannot be paid again.
    // - If the save fails after a successful charge (for example the hold expired and
    //   someone else took the seat), the customer is charged without getting the seat.
    //
    // Full write-up: docs/chapters/03-duplicate-payment.md
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

        // Already paid: return the same result again without charging (a safe retry).
        if (reservation.Status == ReservationStatus.Confirmed)
        {
            return ToDto(reservation);
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

        // 2) Claim the payment BEFORE taking money. The database accepts only one
        // payment row per reservation, so only one request gets past this point.
        var claim = new Payment { ReservationId = reservation.Id, CreatedAtUtc = now };
        if (!await _paymentRepository.TryAddAsync(claim))
        {
            throw new ConflictException("A payment for this reservation already exists or is in progress.");
        }

        // Take the money. No database connection is held while we wait.
        PaymentResultDto payment;
        try
        {
            payment = await _paymentGateway.ChargeAsync(reservation.Id, reservation.TotalInKurus);
        }
        catch (PaymentFailedException)
        {
            // The provider said no, so the user may try again.
            // Any other error (timeout, lost connection) keeps the claim: we do not know
            // whether the money was taken, and charging again would be worse.
            await _paymentRepository.RemoveAsync(claim);
            throw;
        }

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
