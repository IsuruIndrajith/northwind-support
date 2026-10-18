using System.Linq;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Selects an agent for a ticket based on workload, specialization, and capacity.
/// Implements BR-4 and BR-5:
/// - BR-4: A category with RequiresSpecialist may only go to an agent with that specialization
/// - BR-5: Eligible agents: active, specialised where BR-4 applies, and strictly below MaxOpenTickets.
// "Open" means status not Resolved/Closed and is counted from the tickets themselves.
// Choose the fewest open tickets, with a deterministic tie-break (lowest agent id).
/// If nobody is eligible, create the ticket unassigned with a reason.
/// </summary>
public sealed class AgentSelector : IAgentSelector
{
    /// <summary>
    /// Selects the most appropriate agent for a ticket.
    /// </summary>
    /// <param name="candidates">Available agents.</param>
    /// <param name="category">The ticket's category.</param>
    /// <param name="getOpenTicketCount">Function to get open ticket count for an agent.</param>
    /// <returns>The selected agent ID, or null if no eligible agents with a reason.</returns>
    public (int? AgentId, string Reason) SelectAgent(
        IReadOnlyList<Agent> candidates,
        Category category,
        Func<Agent, int> getOpenTicketCount)
    {
        // Filter candidates based on eligibility criteria
        var eligibleAgents = candidates
            // Must be active
            .Where(a => a.IsActive)
            // If category requires specialist, agent must have that specialization
            .Where(a => !category.RequiresSpecialist || a.Specializations.Any(s => s.CategoryId == category.Id))
            // Must be strictly below MaxOpenTickets
            .Where(a => getOpenTicketCount(a) < a.MaxOpenTickets)
            .ToList();

        // If no eligible agents, return null with reason
        if (!eligibleAgents.Any())
        {
            return (null, "No eligible agents available");
        }

        // Select agent with fewest open tickets
        // Tie-break: lowest agent id (deterministic)
        var selectedAgent = eligibleAgents
            .OrderBy(a => getOpenTicketCount(a))
            .ThenBy(a => a.Id)
            .First();

        return (selectedAgent.Id, $"Selected agent with {getOpenTicketCount(selectedAgent)} open tickets");
    }
}