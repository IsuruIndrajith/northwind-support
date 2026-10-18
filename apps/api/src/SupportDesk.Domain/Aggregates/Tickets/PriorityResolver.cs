using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Resolves the final priority for a ticket based on requested priority and category rules.
/// Implements BR-1: requested priority default Medium, category with ForcesCriticalPriority is always Critical.
/// </summary>
public sealed class PriorityResolver : IPriorityResolver
{
    /// <summary>
    /// Resolves the final priority for a ticket.
    /// </summary>
    /// <param name="requestedPriority">The priority requested by the ticket creator (can be null).</param>
    /// <param name="category">The category of the ticket.</param>
    /// <returns>The resolved priority level.</returns>
    public TicketPriority ResolvePriority(TicketPriority? requestedPriority, Category category)
    {
        // BR-1: requested priority, default Medium
        var priority = requestedPriority ?? TicketPriority.Medium;

        // BR-1: A category with ForcesCriticalPriority is always Critical
        if (category.ForcesCriticalPriority)
        {
            return TicketPriority.Critical;
        }

        return priority;
    }
}