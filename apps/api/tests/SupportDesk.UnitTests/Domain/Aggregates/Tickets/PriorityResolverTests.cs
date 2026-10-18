using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class PriorityResolverTests
{
    private readonly PriorityResolver _resolver;

    public PriorityResolverTests()
    {
        _resolver = new PriorityResolver();
    }

    [Fact]
    public void ResolvePriority_WithRequestedPriority_UsesRequestedUnlessForcesCritical()
    {
        // Arrange
        var normalCategory = new Category("Normal", false, false);
        var criticalForcingCategory = new Category("CriticalForcer", false, true);

        // Act & Assert - Normal category
        Assert.Equal(TicketPriority.Low, _resolver.ResolvePriority(TicketPriority.Low, normalCategory));
        Assert.Equal(TicketPriority.Medium, _resolver.ResolvePriority(TicketPriority.Medium, normalCategory));
        Assert.Equal(TicketPriority.High, _resolver.ResolvePriority(TicketPriority.High, normalCategory));
        Assert.Equal(TicketPriority.Critical, _resolver.ResolvePriority(TicketPriority.Critical, normalCategory));

        // Act & Assert - Critical forcing category overrides requested priority
        Assert.Equal(TicketPriority.Critical, _resolver.ResolvePriority(TicketPriority.Low, criticalForcingCategory));
        Assert.Equal(TicketPriority.Critical, _resolver.ResolvePriority(TicketPriority.Medium, criticalForcingCategory));
        Assert.Equal(TicketPriority.Critical, _resolver.ResolvePriority(TicketPriority.High, criticalForcingCategory));
        Assert.Equal(TicketPriority.Critical, _resolver.ResolvePriority(TicketPriority.Critical, criticalForcingCategory));
    }

    [Fact]
    public void ResolvePriority_WithoutRequestedPriority_DefaultsToMediumUnlessForcesCritical()
    {
        // Arrange
        var normalCategory = new Category("Normal", false, false);
        var criticalForcingCategory = new Category("CriticalForcer", false, true);

        // Act & Assert
        Assert.Equal(TicketPriority.Medium, _resolver.ResolvePriority(null, normalCategory));
        Assert.Equal(TicketPriority.Critical, _resolver.ResolvePriority(null, criticalForcingCategory));
    }
}