using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketBooking.Domain.Entities;

namespace TicketBooking.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(r => r.Event)
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Seat)
            .WithMany()
            .HasForeignKey(r => r.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.UserId);
    }
}
