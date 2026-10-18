using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Calculates SLA windows and due dates for tickets based on priority and customer tier.
/// </summary>
public interface ISlaWindowCalculator
{
    /// <summary>
    /// Calculates the due date for a ticket based on its creation time, priority, and customer tier.
    /// </summary>
    /// <param name="createdAtUtc">The time the ticket was created.</param>
    /// <param name="priority">The ticket's priority level.</param>
    /// <param name="isPremiumCustomer">Whether the customer is premium tier.</param>
    /// <returns>The calculated due date in UTC.</returns>
    DateTime CalculateDueDate(DateTime createdAtUtc, TicketPriority priority, bool isPremiumCustomer);
}