using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketBooking.Domain.Entities;

namespace TicketBooking.Infrastructure.Persistence.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Section).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Row).HasMaxLength(10).IsRequired();

        // Stored as text ("Available", "Held", "Sold") so the table is readable in SQL.
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        // Chapter 1 fix: map Version to PostgreSQL's built-in xmin column.
        // EF Core adds "AND xmin = <value read>" to every UPDATE of a seat,
        // so if another request changed the seat in the meantime, the UPDATE hits 0 rows.
        builder.Property(s => s.Version).IsRowVersion();

        // The same physical seat cannot exist twice in one event.
        builder.HasIndex(s => new { s.EventId, s.Section, s.Row, s.Number }).IsUnique();
    }
}
