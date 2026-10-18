namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Request to escalate a ticket.
/// </summary>
public sealed record EscalateTicketRequest(
    /// <summary>The reason for escalation (5-500 characters).</summary>
    string Reason,

    /// <summary>The ID of the user performing the escalation.</summary>
    int EscalatedBy);