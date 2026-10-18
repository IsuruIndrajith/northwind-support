namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Contains human-readable reasons for how a ticket was triaged (priority, SLA window, and assignment).
/// </summary>
public sealed record TriageDto
{
    /// <summary>
    /// Explanation of how the ticket's priority was determined.
    /// </summary>
    public string PriorityReason { get; init; } = string.Empty;

    /// <summary>
    /// Explanation of how the ticket's SLA window was calculated.
    /// </summary>
    public string SlaWindowReason { get; init; } = string.Empty;

    /// <summary>
    /// Explanation of how the ticket was assigned to an agent (or why it was left unassigned).
    /// </summary>
    public string AssignmentReason { get; init; } = string.Empty;
}