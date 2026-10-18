using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>
/// Gets the escalation history for a ticket.
/// </summary>
public sealed class GetTicketEscalationsQueryHandler(ITicketEscalationQueries escalationQueries)
{
    /// <summary>
    /// Gets the escalation history for a ticket, ordered newest first.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The escalation records for the ticket, ordered newest first.</returns>
    public Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct) =>
        escalationQueries.GetEscalationsAsync(ticketId, ct);
}