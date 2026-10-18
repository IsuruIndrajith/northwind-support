using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Calculates SLA windows and due dates for tickets based on priority and customer tier.
/// Implements BR-2, BR-3, BR-6.
/// </summary>
public sealed class SlaWindowCalculator : ISlaWindowCalculator
{
    private readonly Dictionary<TicketPriority, int> _baseWindowHours;
    private readonly double _premiumCustomerMultiplier;
    private readonly double _minimumWindowHours;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlaWindowCalculator"/> class.
    /// </summary>
    /// <param name="baseWindowHours">Base window hours for each priority level.</param>
    /// <param name="premiumCustomerMultiplier">Multiplier applied to window for premium customers (BR-3).</param>
    /// <param name="minimumWindowHours">Minimum window in hours after applying premium multiplier (BR-3).</param>
    public SlaWindowCalculator(
        Dictionary<TicketPriority, int> baseWindowHours,
        double premiumCustomerMultiplier,
        double minimumWindowHours)
    {
        _baseWindowHours = baseWindowHours;
        _premiumCustomerMultiplier = premiumCustomerMultiplier;
        _minimumWindowHours = minimumWindowHours;
    }

    /// <summary>
    /// Calculates the due date for a ticket based on its creation time, priority, and customer tier.
    /// </summary>
    /// <param name="createdAtUtc">The time the ticket was created.</param>
    /// <param name="priority">The ticket's priority level.</param>
    /// <param name="isPremiumCustomer">Whether the customer is premium tier.</param>
    /// <returns>The calculated due date in UTC.</returns>
    public DateTime CalculateDueDate(DateTime createdAtUtc, TicketPriority priority, bool isPremiumCustomer)
    {
        // Get base window in hours for the priority (BR-2)
        int baseWindowHours = _baseWindowHours[priority];

        // Apply premium customer multiplier (BR-3)
        double effectiveWindowHours = baseWindowHours;
        if (isPremiumCustomer)
        {
            effectiveWindowHours *= _premiumCustomerMultiplier;
        }

        // Apply minimum window of 1 hour (BR-3)
        effectiveWindowHours = Math.Max(effectiveWindowHours, _minimumWindowHours);

        // Calculate due date
        return createdAtUtc.AddHours(effectiveWindowHours);
    }
}