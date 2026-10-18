using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Implementation of ticket escalation queries using EF Core.
/// </summary>
public sealed class TicketEscalationQueries(SupportDbContext db) : ITicketEscalationQueries
{
    /// <summary>
    /// Gets the escalation history for a ticket, ordered newest first.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The escalation records for the ticket, ordered newest first.</returns>
    public Task<IReadOnlyList<TicketEscalationDto>> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        return db.Set<TicketEscalation>()
            .Where(te => te.TicketId == ticketId)
            .OrderByDescending(te => te.EscalatedAtUtc)
            .Select(te => new TicketEscalationDto(
                te.Id,
                te.TicketId,
                te.FromPriority,
                te.ToPriority,
                te.FromAgentId == null ? null :
                    db.Set<Agent>().Where(a => a.Id == te.FromAgentId.Value)
                        .Select(a => new AgentSummaryDto(a.Id, a.FullName))
                        .FirstOrDefault(),
                te.ToAgentId == null ? null :
                    db.Set<Agent>().Where(a => a.Id == te.ToAgentId.Value)
                        .Select(a => new AgentSummaryDto(a.Id, a.FullName))
                        .FirstOrDefault(),
                te.FromDueAtUtc,
                te.ToDueAtUtc,
                te.Reason,
                te.EscalatedBy,
                te.EscalatedAtUtc))
            .AsNoTracking()
            .ToListAsync(ct);
    }
}