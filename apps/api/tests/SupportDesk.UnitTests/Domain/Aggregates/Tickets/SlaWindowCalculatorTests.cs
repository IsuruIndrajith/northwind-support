using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaWindowCalculatorTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SlaWindowCalculator _calculator;

    public SlaWindowCalculatorTests()
    {
        _calculator = new SlaWindowCalculator(
            new Dictionary<TicketPriority, int>
            {
                [TicketPriority.Low] = 72,
                [TicketPriority.Medium] = 24,
                [TicketPriority.High] = 8,
                [TicketPriority.Critical] = 4
            },
            0.5, // PremiumCustomerMultiplier
            1.0  // MinimumWindowHours
        );
    }

    [Fact]
    public void CalculateDueDate_NonPremiumCustomer_UsesBaseWindow()
    {
        // Act & Assert
        Assert.Equal(FixedNow.AddHours(24), _calculator.CalculateDueDate(FixedNow, TicketPriority.Medium, false));
        Assert.Equal(FixedNow.AddHours(8), _calculator.CalculateDueDate(FixedNow, TicketPriority.High, false));
        Assert.Equal(FixedNow.AddHours(4), _calculator.CalculateDueDate(FixedNow, TicketPriority.Critical, false));
        Assert.Equal(FixedNow.AddHours(72), _calculator.CalculateDueDate(FixedNow, TicketPriority.Low, false));
    }

    [Fact]
    public void CalculateDueDate_PremiumCustomer_GetsHalfWindow()
    {
        // Act & Assert
        Assert.Equal(FixedNow.AddHours(12), _calculator.CalculateDueDate(FixedNow, TicketPriority.Medium, true)); // 24 * 0.5
        Assert.Equal(FixedNow.AddHours(4), _calculator.CalculateDueDate(FixedNow, TicketPriority.High, true));   // 8 * 0.5
        Assert.Equal(FixedNow.AddHours(2), _calculator.CalculateDueDate(FixedNow, TicketPriority.Critical, true)); // 4 * 0.5
        Assert.Equal(FixedNow.AddHours(36), _calculator.CalculateDueDate(FixedNow, TicketPriority.Low, true));   // 72 * 0.5
    }

    [Fact]
    public void CalculateDueDate_PremiumCustomer_RespectsMinimumWindow()
    {
        // For Critical priority with premium: 4 * 0.5 = 2 hours, but minimum is 1 hour, so should be 2 hours
        // Actually 2 hours is already above minimum, so let's test a case that would go below minimum
        // We need to test with a custom option where the calculation would go below 1 hour

        var calculator = new SlaWindowCalculator(
            new Dictionary<TicketPriority, int>
            {
                [TicketPriority.Critical] = 1 // 1 hour base
            },
            0.5, // Would give 0.5 hours
            1.0  // But minimum is 1 hour
        );

        // Should be 1 hour (minimum) not 0.5 hours
        Assert.Equal(FixedNow.AddHours(1), calculator.CalculateDueDate(FixedNow, TicketPriority.Critical, true));
    }

    [Fact]
    public void CalculateDueDate_ZeroBaseWindow_UsesMinimumWindow()
    {
        var calculator = new SlaWindowCalculator(
            new Dictionary<TicketPriority, int>
            {
                [TicketPriority.Low] = 0 // 0 hours base
            },
            0.5,
            1.0 // Minimum is 1 hour
        );

        // Should be 1 hour (minimum) not 0 hours
        Assert.Equal(FixedNow.AddHours(1), calculator.CalculateDueDate(FixedNow, TicketPriority.Low, false));
    }
}