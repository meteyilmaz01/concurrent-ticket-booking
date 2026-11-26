# Chapter 3 — Duplicate payment

> **Problem:** pressing "Pay" again while the first payment is still running charges the customer again.
> **Fix:** save a payment row with a unique constraint *before* taking the money.
> **Tags:** [`ch3-broken`](https://github.com/meteyilmaz01/concurrent-ticket-booking/tree/ch3-broken) → [`ch3-fixed`](https://github.com/meteyilmaz01/concurrent-ticket-booking/tree/ch3-fixed)

[← Back to README](../../README.md)

## The scenario

A payment takes about 2 seconds. Meanwhile the user clicks "Pay" again, or the app retries because the answer is late. Several confirm requests for the same reservation are now running at once. The customer must be charged exactly once.

## The broken code

This is the code that came out of [Chapter 2](02-payment-inside-transaction.md): fast under load, and wrong here.

```csharp
if (reservation.Status != ReservationStatus.Pending)
{
    throw new ConflictException("Reservation is not pending.");
}

// ... more checks ...

var payment = await _paymentGateway.ChargeAsync(reservation.Id, reservation.TotalInKurus);

reservation.Status = ReservationStatus.Confirmed;
// ...
await _reservationRepository.SaveChangesAsync();
```

### Why it looks correct

There is a status check, and confirming a paid reservation a second time returns `409`. Tested one request after another, it never charges twice.

### What actually goes wrong

```
Request A                              Request B
reads reservation → Pending
                                       reads reservation → Pending
charges the customer ✔
                                       charges the customer ✔   ← second charge
saves → 200
                                       saves → 409 (rejected by xmin)
```

It is the check-then-act race from [Chapter 1](01-overselling.md) again, with one difference: the action in the middle cannot be undone. The row version protects the database, not the customer's money.

## Proof

### Integration test

[`DoublePaymentTests`](../../tests/TicketBooking.IntegrationTests/DoublePaymentTests.cs) reserves a seat and sends 10 confirm requests at the same moment. The payment provider is replaced by a small class that counts the calls; the API and PostgreSQL are real. Output at `ch3-broken` (Docker lines and file paths removed; the CLI language is Turkish: *başarısız* = failed, *başarılı* = passed, *toplam* = total):

```
başarısız DoublePaymentTests.Parallel_confirms_of_one_reservation_charge_the_customer_only_once
  The customer must be charged exactly once.
  10 confirm requests: 10 charges, 1 successful responses.

Test çalıştırması özeti: Başarısız!
  toplam: 2
  başarısız oldu: 1
  başarılı: 1
```

### Demo script

[`scripts/double-charge-demo.cs`](../../scripts/double-charge-demo.cs) does the same against the running API and the fake payment gateway, then asks the gateway how often money was taken:

```
Seat A-1-2 reserved. Sending 10 concurrent payment requests.
  200 OK       : 1
  409 Conflict : 9
  Charges at the payment gateway : 10
  Ticket price : 1500 TRY, money taken : 15000 TRY
Expected exactly 1 charge. Got 10. CHARGED MORE THAN ONCE
```

The customer paid ten times and was told nine times that it did not work.

## Tempting wrong fixes

- **Disable the button in the frontend.** It does not stop retries, other clients or a second tab.
- **"Check whether a payment exists, then charge."** The same race: both requests find nothing.

## The fix

**1. A `payments` table with a unique constraint** ([`PaymentConfiguration`](../../src/TicketBooking.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs)):

```csharp
builder.HasIndex(p => p.ReservationId).IsUnique();
```

**2. Claim first, charge second** ([`ReservationService.ConfirmAsync`](../../src/TicketBooking.Application/Services/ReservationService.cs)):

```csharp
var claim = new Payment { ReservationId = reservation.Id, CreatedAtUtc = now };
if (!await _paymentRepository.TryAddAsync(claim))
{
    throw new ConflictException("A payment for this reservation already exists or is in progress.");
}

try
{
    payment = await _paymentGateway.ChargeAsync(reservation.Id, reservation.TotalInKurus);
}
catch (PaymentFailedException)
{
    await _paymentRepository.RemoveAsync(claim);   // declined: the user may try again
    throw;
}
```

`TryAddAsync` saves immediately and returns `false` when PostgreSQL reports a unique violation. The database decides who wins, so this also holds with several API instances.

**3. A retry after success returns the same result:**

```csharp
if (reservation.Status == ReservationStatus.Confirmed)
{
    return ToDto(reservation);   // same payment id, no new charge
}
```

Repeating a request without changing its effect is called **idempotency**.

| Second request arrives while the first payment is… | Response |
|---|---|
| still running | `409`, no charge |
| successful | `200` with the same payment, no charge |
| declined | the claim is gone, a new attempt is allowed |

## After the fix

```
Seat A-1-5 reserved. Sending 10 concurrent payment requests.
  200 OK       : 1
  409 Conflict : 9
  Charges at the payment gateway : 1
  Ticket price : 1500 TRY, money taken : 1500 TRY
Charged exactly once. Correct.
```

All integration tests pass, including a retry after success and a retry after a declined payment.

## Why not an `Idempotency-Key` header?

Many payment APIs let the client send a key with each request. It is useful for returning the exact earlier response, but a client that creates a new key for every click would still be charged twice. Here the rule is "one reservation, one payment", and the reservation id already expresses it without any help from the client.

## What this fix does not solve

- **Unknown outcome.** If the provider cannot be reached, times out, or the API crashes during the charge, the claim stays and the reservation cannot be paid again. This also happens when the provider is simply down. It is the cautious choice: it never charges twice, but someone has to compare with the provider and release the claim.
- **Declined versus unknown.** Every error response from the gateway is treated as "no money taken". That is true for the fake gateway; with a real provider it has to be checked for each status code.
- **Charged without a seat.** If saving fails after a successful charge, the customer needs a refund.

A real system handles these with reconciliation and by passing an idempotency key to the provider; both are outside the scope of this project.

## Try it yourself

```bash
git checkout ch3-broken
dotnet test --project tests/TicketBooking.IntegrationTests   # fails: charged 10 times

git checkout ch3-fixed
dotnet test --project tests/TicketBooking.IntegrationTests   # passes
```

For the demo script, start the fake payment gateway and the API first (see [Getting started](../../README.md#getting-started)).
