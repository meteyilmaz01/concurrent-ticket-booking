using TicketBooking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
