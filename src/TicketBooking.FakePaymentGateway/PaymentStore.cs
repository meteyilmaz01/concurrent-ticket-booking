using System.Collections.Concurrent;

namespace TicketBooking.FakePaymentGateway;

// Keeps every successful charge in memory, so we can see later how many times
// money was taken for the same reservation.
public class PaymentStore
{
    private readonly ConcurrentQueue<Payment> _payments = new();

    public void Add(Payment payment)
    {
        _payments.Enqueue(payment);
    }

    public List<Payment> GetAll()
    {
        return _payments.ToList();
    }
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public long AmountInKurus { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
