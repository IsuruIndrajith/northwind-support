using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

/// <summary>
/// Loads and stores <see cref="TicketEscalation"/> aggregates. Loaded escalations are tracked, so the unit
/// of work saves whatever their methods changed.
/// </summary>
public sealed class TicketEscalationRepository(SupportDbContext db) : ITicketEscalationRepository
{
    public Task AddAsync(TicketEscalation escalation, CancellationToken ct) =>
        db.Set<TicketEscalation>().AddAsync(escalation, ct);
}