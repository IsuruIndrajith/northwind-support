using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ISlaWindowCalculator slaWindowCalculator,
    IPriorityResolver priorityResolver,
    IAgentSelector agentSelector,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var customer = await customers.GetAsync(request.CustomerId, ct);
        var category = await categories.GetAsync(request.CategoryId, ct);
        var nowUtc = clock.UtcNow;

        // Determine the final priority using the priority resolver (BR-1)
        var priority = priorityResolver.ResolvePriority(request.RequestedPriority, category);

        // Calculate the due date using the SLA window calculator (BR-2, BR-3)
        var isPremium = customer.Tier == CustomerTier.Premium;
        var dueAtUtc = slaWindowCalculator.CalculateDueDate(nowUtc, priority, isPremium);

        // Get agents with their current workload
        var agentDtos = await agentQueries.GetAllAsync(ct);

        // Select an agent using the agent selector (BR-4, BR-5)
        var (assignedAgentId, assignmentReason) = agentSelector.SelectAgent(
            agentDtos.Select(dto => new Agent(
                dto.Id,
                dto.FullName,
                dto.Email,
                dto.MaxOpenTickets,
                dto.CreatedAtUtc)).ToList(),
            category,
            agent => {
                // Find the corresponding DTO to get the open ticket count
                var dto = agentDtos.FirstOrDefault(a => a.Id == agent.Id);
                return dto?.OpenTicketCount ?? 0;
            });

        // Create the ticket with calculated values
        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            nowUtc,
            assignedAgentId,
            dueAtUtc);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        var ticketDetail = await getTicket.HandleAsync(ticket.Id, ct);

        // Add triage information to the response
        ticketDetail.Triage = new TriageDto
        {
            PriorityReason = request.RequestedPriority.HasValue
                ? $"Priority set to {request.RequestedPriority.Value} as requested"
                : "Priority defaulted to Medium",
            SlaWindowReason = (customer.Tier == CustomerTier.Premium)
                ? $"Premium customer receives 50% of standard window (minimum 1 hour)"
                : "Standard SLA window applied",
            AssignmentReason = assignmentReason
        };

        return ticketDetail;
    }
}
