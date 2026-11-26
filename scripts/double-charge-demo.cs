#:property PublishAot=false

// Chapter 3 demo: presses "Pay" many times at once for one reservation,
// then asks the payment gateway how many times the money was actually taken.
// Usage (with the fake payment gateway and the API running):  dotnet run scripts/double-charge-demo.cs

using System.Net;
using System.Net.Http.Json;

var api = new HttpClient { BaseAddress = new Uri("http://localhost:5287") };
var gateway = new HttpClient { BaseAddress = new Uri("http://localhost:5288") };
var requestCount = 10;

// 1) Reserve a free seat
var events = await api.GetFromJsonAsync<List<EventDto>>("/api/events");
var seats = await api.GetFromJsonAsync<List<SeatDto>>($"/api/events/{events![0].Id}/seats");
var seat = seats!.First(s => s.Status == "Available");

var response = await api.PostAsJsonAsync("/api/reservations", new { seatId = seat.Id, userId = Guid.NewGuid() });
response.EnsureSuccessStatusCode();
var reservation = await response.Content.ReadFromJsonAsync<ReservationDto>();

Console.WriteLine($"Seat {seat.Section}-{seat.Row}-{seat.Number} reserved. Sending {requestCount} concurrent payment requests.");

// 2) Pay for the same reservation many times at once
var tasks = new List<Task<HttpResponseMessage>>();
for (var i = 0; i < requestCount; i++)
{
    tasks.Add(api.PostAsync($"/api/reservations/{reservation!.Id}/confirm", null));
}

var responses = await Task.WhenAll(tasks);
var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
var conflicts = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

// 3) Ask the payment gateway what really happened
var payments = await gateway.GetFromJsonAsync<List<PaymentDto>>("/payments");
var charges = payments!.Where(p => p.ReservationId == reservation!.Id).ToList();
var total = charges.Sum(p => p.AmountInKurus);

Console.WriteLine($"  200 OK       : {ok}");
Console.WriteLine($"  409 Conflict : {conflicts}");
Console.WriteLine($"  Charges at the payment gateway : {charges.Count}");
Console.WriteLine($"  Ticket price : {reservation!.TotalInKurus / 100} TRY, money taken : {total / 100} TRY");
Console.WriteLine(charges.Count == 1
    ? "Charged exactly once. Correct."
    : $"Expected exactly 1 charge. Got {charges.Count}. CHARGED MORE THAN ONCE");

record EventDto(Guid Id, string Name);
record SeatDto(Guid Id, string Section, string Row, int Number, string Status);
record ReservationDto(Guid Id, long TotalInKurus);
record PaymentDto(Guid ReservationId, long AmountInKurus);
