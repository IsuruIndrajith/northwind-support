using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Selects an agent for a ticket based on workload, specialization, and capacity.
/// Implements BR-4 and BR-5.
/// </summary>
public interface IAgentSelector
{
    /// <summary>
    /// Selects the most appropriate agent for a ticket.
    /// </summary>
    /// <param name="candidates">Available agents.</param>
    /// <param name="category">The ticket's category.</param>
    /// <param name="getOpenTicketCount">Function to get open ticket count for an agent.</param>
    /// <returns>The selected agent ID, or null if no eligible agents with a reason.</returns>
    (int? AgentId, string Reason) SelectAgent(
        IReadOnlyList<Agent> candidates,
        Category category,
        Func<Agent, int> getOpenTicketCount);
}