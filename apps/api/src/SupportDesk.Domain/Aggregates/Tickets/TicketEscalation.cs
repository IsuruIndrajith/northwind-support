using SupportDesk.Domain.Common;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Records the history of a ticket escalation.
/// </summary>
public sealed class TicketEscalation : AggregateRoot
{
    private TicketEscalation()
    {
        // For EF Core.
    }

    /// <summary>
    /// Records a ticket escalation.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket being escalated.</param>
    /// <param name="fromPriority">The priority before escalation.</param>
    /// <param name="toPriority">The priority after escalation.</param>
    /// <param name="fromAgentId">The agent ID before escalation (can be null).</param>
    /// <param name="toAgentId">The agent ID after escalation (can be null).</param>
    /// <param name="fromDueAtUtc">The due date before escalation (can be null).</param>
    /// <param name="toDueAtUtc">The due date after escalation (can be null).</param>
    /// <param name="reason">The reason for escalation (5-500 characters).</param>
    /// <param name="escalatedBy">The ID of the user who performed the escalation.</param>
    /// <param name="escalatedAtUtc">The date and time of the escalation.</param>
    public static TicketEscalation Create(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime? toDueAtUtc,
        string reason,
        int escalatedBy,
        DateTime escalatedAtUtc)
    {
        // Validate reason length (5-500 characters as per requirements)
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length < 5 || reason.Length > 500)
        {
            throw new ArgumentException("Reason must be between 5 and 500 characters.", nameof(reason));
        }

        return new TicketEscalation
        {
            TicketId = ticketId,
            FromPriority = fromPriority,
            ToPriority = toPriority,
            FromAgentId = fromAgentId,
            ToAgentId = toAgentId,
            FromDueAtUtc = fromDueAtUtc,
            ToDueAtUtc = toDueAtUtc,
            Reason = reason.Trim(),
            EscalatedBy = escalatedBy,
            EscalatedAtUtc = escalatedAtUtc
        };
    }

    public int TicketId { get; private set; }
    public TicketPriority FromPriority { get; private set; }
    public TicketPriority ToPriority { get; private set; }
    public int? FromAgentId { get; private set; }
    public int? ToAgentId { get; private set; }
    public DateTime? FromDueAtUtc { get; private set; }
    public DateTime? ToDueAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public int EscalatedBy { get; private set; }
    public DateTime EscalatedAtUtc { get; private set; }
}