using Microsoft.EntityFrameworkCore;
using TicketBooking.Api.Middlewares;
using TicketBooking.Application.Interfaces.ExternalServices;
using TicketBooking.Application.Interfaces.Repositories;
using TicketBooking.Application.Interfaces.Services;
using TicketBooking.Application.Services;
using TicketBooking.Infrastructure.ExternalServices;
using TicketBooking.Infrastructure.Persistence;
using TicketBooking.Infrastructure.Repository;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Database"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<ISeatRepository, SeatRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IReservationService, ReservationService>();

builder.Services.AddHttpClient<IPaymentGateway, HttpPaymentGateway>(client =>
    client.BaseAddress = new Uri(builder.Configuration["PaymentGateway:BaseUrl"]!));

builder.Services.AddControllers();

var app = builder.Build();

// Development: apply migrations and add demo data
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.AddDemoEventAsync(db);
    await SeedData.AddLoadTestEventAsync(db);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();

app.Run();
