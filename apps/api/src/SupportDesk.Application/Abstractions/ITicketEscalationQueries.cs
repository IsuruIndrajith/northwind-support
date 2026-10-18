using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of ticket escalations. Projects straight to DTOs in the database; never tracks.
/// </summary>
public interface ITicketEscalationQueries
{
    /// <summary>
    /// Gets the escalation history for a ticket, ordered newest first.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The escalation records for the ticket, ordered newest first.</returns>
    Task<IReadOnlyList<TicketEscalationDto>> GetEscalationsAsync(int ticketId, CancellationToken ct);
}