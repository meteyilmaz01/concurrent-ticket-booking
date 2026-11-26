using TicketBooking.FakePaymentGateway;

// A stand-in for a real payment provider (Chapter 2+).
// It is slow on purpose and can be told to fail, so we can see how our API behaves.
//
// Settings (appsettings.json or command line):
//   FakePayment:DelayMs      how long each payment takes (default 2000)
//   FakePayment:FailureRate  share of payments that fail, 0.0 - 1.0 (default 0.0)
//
// Example: dotnet run --project src/TicketBooking.FakePaymentGateway -- --FakePayment:DelayMs=30000

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<PaymentStore>();

var app = builder.Build();

app.MapControllers();

app.Run();
