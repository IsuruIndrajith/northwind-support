using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration for the TicketEscalation entity.
/// </summary>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", t =>
        {
            // No additional table-level constraints needed beyond column constraints
        });

        builder.HasKey(te => te.Id);

        builder.Property(te => te.Reason)
            .IsRequired()
            .HasMaxLength(500); // As per requirements: Reason 500

        builder.Property(te => te.EscalatedBy)
            .IsRequired()
            .HasMaxLength(100); // As per requirements: EscalatedBy 100

        builder.Property(te => te.TicketId)
            .IsRequired();

        builder.Property(te => te.FromPriority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(te => te.ToPriority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(te => te.FromAgentId)
            .IsRequired(false);

        builder.Property(te => te.ToAgentId)
            .IsRequired(false);

        builder.Property(te => te.FromDueAtUtc)
            .IsRequired(false)
            .HasColumnType("datetime2(3)");

        builder.Property(te => te.ToDueAtUtc)
            .IsRequired(false)
            .HasColumnType("datetime2(3)");

        builder.Property(te => te.EscalatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(3)");

        // Configure foreign key to Ticket
        builder.HasOne<SupportDesk.Domain.Aggregates.Tickets.Ticket>()
            .WithMany() // Ticket doesn't need a navigation property to its escalations for this use case
            .HasForeignKey(te => te.TicketId)
            .OnDelete(DeleteBehavior.Cascade); // If ticket is deleted, its escalations should be deleted too

        // Index on TicketId for efficient lookup of escalations for a ticket
        builder.HasIndex(te => te.TicketId)
            .HasDatabaseName("IX_TicketEscalations_TicketId");

        // Index on EscalatedAtUtc for efficient ordering in GET /api/tickets/{id}/escalations (newest first)
        builder.HasIndex(te => te.EscalatedAtUtc)
            .HasDatabaseName("IX_TicketEscalations_EscalatedAtUtc")
            .IsDescending(); // Newest first as per requirements
    }
}