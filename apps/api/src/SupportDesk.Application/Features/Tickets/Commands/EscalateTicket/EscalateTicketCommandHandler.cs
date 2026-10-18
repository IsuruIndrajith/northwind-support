using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    ITicketEscalationRepository escalations,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    IEscalationService escalationService,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="InvalidOperationException">If the ticket cannot be escalated (Critical or Resolved/Closed).</exception>
    public async Task<TicketDetailDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        // Get the current ticket
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Get related data needed for escalation logic
        var customer = await customers.GetAsync(ticket.CustomerId, ct);
        var category = await categories.GetAsync(ticket.CategoryId, ct);
        var nowUtc = clock.UtcNow;

        // Get agents with their current workload
        var agentDtos = await agentQueries.GetAllAsync(ct);

        // Apply the escalation business logic
        var (updatedTicket, escalationRecord) = escalationService.EscalateTicket(
            ticket,
            category,
            customer,
            agentDtos.Select(dto => new Agent(
                dto.Id,
                dto.FullName,
                dto.Email,
                dto.MaxOpenTickets,
                dto.CreatedAtUtc)).ToList(),
            agent => {
                // Find the corresponding DTO to get the open ticket count
                var dto = agentDtos.FirstOrDefault(a => a.Id == agent.Id);
                return dto?.OpenTicketCount ?? 0;
            },
            nowUtc,
            request.Reason,
            request.EscalatedBy);

        // Update the ticket properties (the ticket is tracked by the context from GetByIdAsync)
        ticket.Priority = updatedTicket.Priority;
        ticket.AssignedAgentId = updatedTicket.AssignedAgentId;
        ticket.DueAtUtc = updatedTicket.DueAtUtc;
        ticket.UpdatedAtUtc = updatedTicket.UpdatedAtUtc;

        // Save the escalation record
        await escalations.AddAsync(escalationRecord, ct);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated by user {EscalatedBy}. Reason: {Reason}",
            ticket.Reference, request.EscalatedBy, request.Reason);

        // Return the updated ticket
        return await getTicket.HandleAsync(updatedTicket.Id, ct);
    }
}