using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// A ticket escalation record for history purposes.
/// </summary>
public sealed record TicketEscalationDto(
    /// <summary>The unique ID of the escalation record.</summary>
    int Id,

    /// <summary>The ID of the ticket that was escalated.</summary>
    int TicketId,

    /// <summary>The priority level before escalation.</summary>
    TicketPriority FromPriority,

    /// <summary>The priority level after escalation.</summary>
    TicketPriority ToPriority,

    /// <summary>The agent assigned before escalation (can be null).</summary>
    AgentSummaryDto? FromAgent,

    /// <summary>The agent assigned after escalation (can be null).</summary>
    AgentSummaryDto? ToAgent,

    /// <summary>The due date before escalation (can be null).</summary>
    DateTime? FromDueAtUtc,

    /// <summary>The due date after escalation (can be null).</summary>
    DateTime? ToDueAtUtc,

    /// <summary>The reason for escalation (5-500 characters).</summary>
    string Reason,

    /// <summary>The ID of the user who performed the escalation.</summary>
    int EscalatedBy,

    /// <summary>The date and time when the escalation occurred.</summary>
    DateTime EscalatedAtUtc);