#:property PublishAot=false

// Chapter 1 demo: sends many reservation requests for the same seat at the same time.
// Usage (with the API running):  dotnet run scripts/oversell-demo.cs

using System.Net;
using System.Net.Http.Json;

var http = new HttpClient { BaseAddress = new Uri("http://localhost:5287") };
var requestCount = 50;

// 1) Find a free seat
var events = await http.GetFromJsonAsync<List<EventDto>>("/api/events");
var seats = await http.GetFromJsonAsync<List<SeatDto>>($"/api/events/{events![0].Id}/seats");
var seat = seats!.First(s => s.Status == "Available");

Console.WriteLine($"Seat {seat.Section}-{seat.Row}-{seat.Number}, {requestCount} concurrent requests");

// 2) Start all requests without waiting (each from a different user)
var tasks = new List<Task<HttpResponseMessage>>();
for (var i = 0; i < requestCount; i++)
{
    var body = new { seatId = seat.Id, userId = Guid.NewGuid() };
    tasks.Add(http.PostAsJsonAsync("/api/reservations", body));
}

// 3) Wait for all of them to finish
var responses = await Task.WhenAll(tasks);

// 4) Count the results
var reserved = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
var conflicts = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

Console.WriteLine($"  200 OK (reserved) : {reserved}");
Console.WriteLine($"  409 Conflict      : {conflicts}");
Console.WriteLine(reserved == 1
    ? "Exactly 1 reservation. Correct."
    : $"Expected exactly 1 reservation. Got {reserved}. OVERSOLD");

record EventDto(Guid Id, string Name);
record SeatDto(Guid Id, string Section, string Row, int Number, string Status);
