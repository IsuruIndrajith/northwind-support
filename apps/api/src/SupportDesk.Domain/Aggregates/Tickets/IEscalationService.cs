using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Handles the business logic for ticket escalation (BR-7).
/// </summary>
public interface IEscalationService
{
    /// <summary>
    /// Escalates a ticket, returning the updated ticket and the escalation record.
    /// </summary>
    /// <param name="ticket">The ticket to escalate.</param>
    /// <param name="category">The ticket's category.</param>
    /// <param name="customer">The ticket's customer.</param>
    /// <param name="agents">Available agents.</param>
    /// <param name="getOpenTicketCount">Function to get open ticket count for an agent (excluding the current ticket if checking current agent eligibility).</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="reason">The reason for escalation (5-500 characters).</param>
    /// <param name="escalatedBy">The ID of the user performing the escalation.</param>
    /// <returns>The updated ticket and the escalation record.</returns>
    /// <exception cref="InvalidOperationException">If the ticket cannot be escalated (Critical or Resolved/Closed).</exception>
    (Ticket Ticket, TicketEscalation Escalation) EscalateTicket(
        Ticket ticket,
        Category category,
        Customer customer,
        IReadOnlyList<Agent> agents,
        Func<Agent, int> getOpenTicketCount,
        DateTime nowUtc,
        string reason,
        int escalatedBy);
}