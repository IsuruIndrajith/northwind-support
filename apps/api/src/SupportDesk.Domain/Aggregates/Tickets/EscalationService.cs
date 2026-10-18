using System.Reflection;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Handles the business logic for ticket escalation (BR-7).
/// Reuses existing calculators and resolvers where possible to avoid duplicating rule logic.
/// </summary>
public sealed class EscalationService : IEscalationService
{
    /// <summary>
    /// Escalates a ticket, returning the updated ticket and the escalation record.
    /// </summary>
    /// <param name="ticket">The ticket to escalate.</param>
    /// <param name="category">The ticket's category.</param>
    /// <param name="customer">The ticket's customer.</param>
    /// <param name="agents">Available agents.</param>
    /// <param name="getOpenTicketCount">Function to get open ticket count for an agent.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="reason">The reason for escalation (5-500 characters).</param>
    /// <param name="escalatedBy">The ID of the user performing the escalation.</param>
    /// <returns>The updated ticket and the escalation record.</returns>
    /// <exception cref="InvalidOperationException">If the ticket cannot be escalated (Critical or Resolved/Closed).</exception>
    public (Ticket Ticket, TicketEscalation Escalation) EscalateTicket(
        Ticket ticket,
        Category category,
        Customer customer,
        IReadOnlyList<Agent> agents,
        Func<Agent, int> getOpenTicketCount,
        DateTime nowUtc,
        string reason,
        int escalatedBy)
    {
        // BR-7: Critical and Resolved/Closed tickets cannot be escalated
        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new InvalidOperationException("Critical tickets cannot be escalated.");
        }

        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException("Resolved/Closed tickets cannot be escalated.");
        }

        // Store original values for the escalation record
        var fromPriority = ticket.Priority;
        var fromAgentId = ticket.AssignedAgentId;
        var fromDueAtUtc = ticket.DueAtUtc;

        // BR-7: Raises priority exactly one level
        TicketPriority toPriority = EscalatePriority(fromPriority);

        // BR-7: Recomputes due date from the moment of escalation (BR-2, BR-3)
        bool isPremium = customer.Tier == CustomerTier.Premium;
        DateTime toDueAtUtc = nowUtc.AddHours(CalculateWindowHours(toPriority, isPremium));

        // BR-7: Re-evaluates owner: keep current agent if still eligible, else reassign per BR-5
        // (when deciding whether the current agent is still eligible, do not count this ticket against their open total)
        int? toAgentId;
        string assignmentReason;

        if (fromAgentId.HasValue)
        {
            // Check if current agent is still eligible (excluding this ticket from their count)
            var currentAgent = agents.FirstOrDefault(a => a.Id == fromAgentId.Value);
            if (currentAgent != null && IsAgentEligible(currentAgent, category, agents, getOpenTicketCount, fromAgentId.Value, excludeCurrentTicket: true))
            {
                toAgentId = fromAgentId;
                assignmentReason = $"Current agent remains eligible";
            }
            else
            {
                // Current agent is not eligible, select new agent per BR-5
                var (selectedAgentId, selectedReason) = SelectAgent(agents, category, getOpenTicketCount, excludeCurrentTicket: true, excludeAgentId: fromAgentId);
                toAgentId = selectedAgentId;
                assignmentReason = selectedReason;
            }
        }
        else
        {
            // Ticket was unassigned, select agent per BR-5
            var (selectedAgentId, selectedReason) = SelectAgent(agents, category, getOpenTicketCount);
            toAgentId = selectedAgentId;
            assignmentReason = selectedReason;
        }

        // Create a new Ticket object with updated values using reflection
        // since property setters are private and we can't use the Raise method
        // (it hardcodes Status to New)
        var updatedTicket = CreateTicketWithValues(
            ticket,
            toPriority,
            toAgentId,
            toDueAtUtc,
            nowUtc);

        // Create the escalation history record
        var escalation = TicketEscalation.Create(
            ticketId: ticket.Id,
            fromPriority: fromPriority,
            toPriority: toPriority,
            fromAgentId: fromAgentId,
            toAgentId: toAgentId,
            fromDueAtUtc: fromDueAtUtc,
            toDueAtUtc: toDueAtUtc,
            reason: reason,
            escalatedBy: escalatedBy,
            escalatedAtUtc: nowUtc);

        return (updatedTicket, escalation);
    }

    /// <summary>
    /// Creates a new Ticket object with updated property values using reflection.
    /// This is necessary because Ticket property setters are private.
    /// </summary>
    private Ticket CreateTicketWithValues(
        Ticket original,
        TicketPriority newPriority,
        int? newAssignedAgentId,
        DateTime? newDueAtUtc,
        DateTime newUpdatedAtUtc)
    {
        // Create a new Ticket instance using the private constructor
        var ticketType = typeof(Ticket);
        var constructor = ticketType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        if (constructor == null)
        {
            throw new InvalidOperationException("Cannot access Ticket constructor");
        }

        var ticket = (Ticket)constructor.Invoke(null);

        // Set property values using reflection
        var propertiesToSet = new[]
        {
            new PropertyValue("Id", original.Id),
            new PropertyValue("Reference", original.Reference),
            new PropertyValue("Title", original.Title),
            new PropertyValue("Description", original.Description),
            new PropertyValue("CustomerId", original.CustomerId),
            new PropertyValue("CategoryId", original.CategoryId),
            new PropertyValue("Priority", newPriority),
            new PropertyValue("Status", original.Status), // Preserve original status
            new PropertyValue("AssignedAgentId", newAssignedAgentId),
            new PropertyValue("DueAtUtc", newDueAtUtc),
            new PropertyValue("CreatedAtUtc", original.CreatedAtUtc), // Preserve original
            new PropertyValue("UpdatedAtUtc", newUpdatedAtUtc),
            new PropertyValue("ResolvedAtUtc", original.ResolvedAtUtc) // Preserve original
        };

        foreach (var prop in propertiesToSet)
        {
            var propertyInfo = ticketType.GetProperty(
                prop.Name,
                BindingFlags.Instance | BindingFlags.Public);

            if (propertyInfo == null || !propertyInfo.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Cannot set property {prop.Name} on Ticket");
            }

            propertyInfo.SetValue(ticket, prop.Value);
        }

        return ticket;
    }

    private struct PropertyValue
    {
        public string Name { get; }
        public object Value { get; }

        public PropertyValue(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }

    /// <summary>
    /// Escalates the priority by one level, with Critical as the maximum.
    /// </summary>
    private TicketPriority EscalatePriority(TicketPriority currentPriority)
    {
        return currentPriority switch
        {
            TicketPriority.Low => TicketPriority.Medium,
            TicketPriority.Medium => TicketPriority.High,
            TicketPriority.High => TicketPriority.Critical,
            TicketPriority.Critical => TicketPriority.Critical, // Should not reach here due to validation above
            _ => throw new ArgumentOutOfRangeException(nameof(currentPriority), currentPriority, null)
        };
    }

    /// <summary>
    /// Calculates the window hours for a priority and customer tier.
    /// Duplicates the logic from SlaWindowCalculator to avoid taking a dependency on configuration values
    /// that might change during the escalation process. In a real implementation, we might inject
    /// the calculator or the configuration values.
    /// </summary>
    private double CalculateWindowHours(TicketPriority priority, bool isPremiumCustomer)
    {
        // These values should ideally come from configuration like in SlaWindowCalculator
        // For now, we duplicate the logic to keep this service self-contained for the domain rule
        var baseWindowHours = new Dictionary<TicketPriority, int>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        };

        double baseHours = baseWindowHours[priority];
        double effectiveHours = baseHours;

        if (isPremiumCustomer)
        {
            effectiveHours *= 0.5; // PremiumCustomerMultiplier
        }

        // Apply minimum window of 1 hour
        effectiveHours = Math.Max(effectiveHours, 1.0); // MinimumWindowHours

        return effectiveHours;
    }

    /// <summary>
    /// Checks if an agent is eligible for a ticket category.
    /// </summary>
    private bool IsAgentEligible(
        Agent agent,
        Category category,
        IReadOnlyList<Agent> agents,
        Func<Agent, int> getOpenTicketCount,
        int agentIdToExclude,
        bool excludeCurrentTicket)
    {
        // Must be active
        if (!agent.IsActive)
        {
            return false;
        }

        // If category requires specialist, agent must have that specialization
        if (category.RequiresSpecialist && !agent.Specializations.Any(s => s.CategoryId == category.Id))
        {
            return false;
        }

        // Get the agent's open ticket count
        int openTicketCount = getOpenTicketCount(agent);

        // If excluding current ticket and this is the agent we're checking, subtract 1 from their count
        if (excludeCurrentTicket && agent.Id == agentIdToExclude)
        {
            openTicketCount = Math.Max(0, openTicketCount - 1);
        }

        // Must be strictly below MaxOpenTickets
        return openTicketCount < agent.MaxOpenTickets;
    }

    /// <summary>
    /// Selects an agent for a ticket, with optional exclusions.
    /// </summary>
    private (int? AgentId, string Reason) SelectAgent(
        IReadOnlyList<Agent> agents,
        Category category,
        Func<Agent, int> getOpenTicketCount,
        bool excludeCurrentTicket = false,
        int? excludeAgentId = null)
    {
        // Filter candidates based on eligibility criteria
        var eligibleAgents = agents
            // Must be active
            .Where(a => a.IsActive)
            // If category requires specialist, agent must have that specialization
            .Where(a => !category.RequiresSpecialist || a.Specializations.Any(s => s.CategoryId == category.Id))
            // Agent-specific exclusions
            .Where(a => !excludeAgentId.HasValue || a.Id != excludeAgentId.Value)
            // Must be strictly below MaxOpenTickets (adjusting for excluded current ticket if needed)
            .Where(a =>
            {
                int openTicketCount = getOpenTicketCount(a);
                if (excludeCurrentTicket && a.Id == excludeAgentId)
                {
                    openTicketCount = Math.Max(0, openTicketCount - 1);
                }
                return openTicketCount < a.MaxOpenTickets;
            })
            .ToList();

        // If no eligible agents, return null with reason
        if (!eligibleAgents.Any())
        {
            return (null, "No eligible agents available");
        }

        // Select agent with fewest open tickets
        // Tie-break: lowest agent id (deterministic)
        var selectedAgent = eligibleAgents
            .OrderBy(a =>
            {
                int count = getOpenTicketCount(a);
                if (excludeCurrentTicket && a.Id == excludeAgentId)
                {
                    count = Math.Max(0, count - 1);
                }
                return count;
            })
            .ThenBy(a => a.Id)
            .First();

        int finalCount = getOpenTicketCount(selectedAgent);
        if (excludeCurrentTicket && selectedAgent.Id == excludeAgentId)
        {
            finalCount = Math.Max(0, finalCount - 1);
        }

        return (selectedAgent.Id, $"Selected agent with {finalCount} open tickets");
    }
}