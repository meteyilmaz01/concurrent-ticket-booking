using Microsoft.AspNetCore.Mvc;

namespace TicketBooking.FakePaymentGateway.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentStore _store;
    private readonly IConfiguration _configuration;

    public PaymentsController(PaymentStore store, IConfiguration configuration)
    {
        _store = store;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest request)
    {
        var delayMs = _configuration.GetValue("FakePayment:DelayMs", 2000);
        var failureRate = _configuration.GetValue("FakePayment:FailureRate", 0.0);

        // Pretend to talk to the bank
        await Task.Delay(delayMs);

        if (Random.Shared.NextDouble() < failureRate)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Payment provider is temporarily unavailable." });
        }

        var payment = new Payment
        {
            Id = Guid.CreateVersion7(),
            ReservationId = request.ReservationId,
            AmountInKurus = request.AmountInKurus,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _store.Add(payment);

        return Ok(payment);
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_store.GetAll());
    }
}

public class ChargeRequest
{
    public Guid ReservationId { get; set; }
    public long AmountInKurus { get; set; }
}
