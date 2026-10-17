using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using Xunit;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Queries;

public class SearchTicketsQueryHandlerTests
{
    private SupportDbContext CreateInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SupportDbContext(options);
        context.Database.EnsureCreated();
        SeedDatabase(context);
        return context;
    }

    private void SeedDatabase(SupportDbContext context)
    {
        var clock = new FixedClock(new DateTime(2026, 1, 1));

        // Customers
        var customer1 = new Customer("Contoso Ltd", "support@contoso.example", "555-0001", CustomerTier.Premium, clock.UtcNow);
        var customer2 = new Customer("Fabrikam Inc", "info@fabrikam.example", "555-0002", CustomerTier.Standard, clock.UtcNow);
        var customer3 = new Customer("Wide World Importers", "wwi@example.com", "555-0003", CustomerTier.Premium, clock.UtcNow);

        context.Set<Customer>().AddRange(customer1, customer2, customer3);
        context.SaveChanges();

        // Categories
        var categoryGeneral = new Category("General", false, false);
        var categoryBilling = new Category("Billing", false, false);
        var categoryTechnical = new Category("Technical", false, false);
        var categoryOutage = new Category("Outage", true, true); // ForcesCriticalPriority, RequiresSpecialist
        var categorySecurity = new Category("Security", true, true);

        context.Set<Category>().AddRange(categoryGeneral, categoryBilling, categoryTechnical, categoryOutage, categorySecurity);
        context.SaveChanges();

        // Agents
        var agent1 = new Agent("Alex Turner", "alex@example.com", 10, clock.UtcNow);
        var agent2 = new Agent("Ben Osei", "ben@example.com", 8, clock.UtcNow);
        var agent3 = new Agent("Priya Nair", "priya@example.com", 8, clock.UtcNow);

        context.Set<Agent>().AddRange(agent1, agent2, agent3);
        context.SaveChanges();

        // Tickets
        var ticket1 = Ticket.Raise(
            "TCK-0042",
            "Login issue",
            "Cannot login to the system",
            customer1.Id,
            categoryTechnical.Id,
            TicketPriority.High,
            clock.UtcNow.AddDays(-2));

        ticket1.AssignTo(agent1.Id, clock.UtcNow);
        ticket1.ChangeStatus(TicketStatus.InProgress, clock.UtcNow);

        var ticket2 = Ticket.Raise(
            "TCK-0043",
            "Invoice query",
            "Question about invoice",
            customer2.Id,
            categoryBilling.Id,
            TicketPriority.Medium,
            clock.UtcNow.AddDays(-1));
        // ticket2 remains unassigned (AssignedAgentId is null)
        ticket2.ChangeStatus(TicketStatus.Open, clock.UtcNow);

        var ticket3 = Ticket.Raise(
            "TCK-0044",
            "Security alert",
            "Potential security breach",
            customer3.Id,
            categorySecurity.Id,
            TicketPriority.Critical,
            clock.UtcNow);

        ticket3.AssignTo(agent2.Id, clock.UtcNow);
        ticket3.ChangeStatus(TicketStatus.Open, clock.UtcNow);

        context.Set<Ticket>().AddRange(ticket1, ticket2, ticket3);
        context.SaveChanges();
    }

    [Fact]
    public async Task GetPagedAsync_ExactMatchPriority_ReturnsOnlyMatchingTickets()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var clock = new FixedClock(new DateTime(2026, 1, 1));
        var ticketQueries = new TicketQueries(context, clock);

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await ticketQueries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
        Assert.Equal("TCK-0042", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_CombinationOfFilters_ReturnsTicketsMatchingAll()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var clock = new FixedClock(new DateTime(2026, 1, 1));
        var ticketQueries = new TicketQueries(context, clock);

        var query = new TicketQuery
        {
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await ticketQueries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(TicketStatus.Open, result.Items[0].Status);
        Assert.Equal(TicketPriority.Medium, result.Items[0].Priority);
        Assert.Equal("TCK-0043", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_SearchOnCustomerName_ReturnsTicketsForThatCustomer()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var clock = new FixedClock(new DateTime(2026, 1, 1));
        var ticketQueries = new TicketQueries(context, clock);

        var query = new TicketQuery
        {
            Search = "Fabrikam",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await ticketQueries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-0043", result.Items[0].Reference);
        Assert.Equal("Fabrikam Inc", result.Items[0].Customer.Name);
    }

    [Fact]
    public async Task GetPagedAsync_UnassignedOnly_ReturnsOnlyUnassignedTickets()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var clock = new FixedClock(new DateTime(2026, 1, 1));
        var ticketQueries = new TicketQueries(context, clock);

        var query = new TicketQuery
        {
            UnassignedOnly = true,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await ticketQueries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Null(result.Items[0].AssignedAgent);
        Assert.Equal("TCK-0043", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_MultipleFiltersCombinedWithAnd_ReturnsTicketsMatchingAll()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var clock = new FixedClock(new DateTime(2026, 1, 1));
        var ticketQueries = new TicketQueries(context, clock);

        var query = new TicketQuery
        {
            Status = TicketStatus.Open,
            Priority = TicketPriority.Critical,
            CategoryId = 5, // Security
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await ticketQueries.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(TicketStatus.Open, result.Items[0].Status);
        Assert.Equal(TicketPriority.Critical, result.Items[0].Priority);
        Assert.Equal(5, result.Items[0].Category.Id);
        Assert.Equal("TCK-0044", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_SearchTrimmedAndBlankIgnored_WorksCorrectly()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var clock = new FixedClock(new DateTime(2026, 1, 1));
        var ticketQueries = new TicketQueries(context, clock);

        // Test with whitespace around search term
        var queryWithWhitespace = new TicketQuery
        {
            Search = "  Contoso  ",
            Page = 1,
            PageSize = 10
        };

        // Act
        var resultWithWhitespace = await ticketQueries.GetPagedAsync(queryWithWhitespace, CancellationToken.None);

        // Assert
        Assert.Equal(1, resultWithWhitespace.TotalCount);
        Assert.Single(resultWithWhitespace.Items);
        Assert.Equal("TCK-0042", resultWithWhitespace.Items[0].Reference);

        // Test with blank search (should return all tickets)
        var queryBlank = new TicketQuery
        {
            Search = "",
            Page = 1,
            PageSize = 10
        };

        // Act
        var resultBlank = await ticketQueries.GetPagedAsync(queryBlank, CancellationToken.None);

        // Assert
        Assert.Equal(3, resultBlank.TotalCount); // All three tickets
    }

    [Fact]
    public async Task GetPagedAsync_TotalCountAndPagingAreCorrectForFilteredSet()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var clock = new FixedClock(new DateTime(2026, 1, 1));
        var ticketQueries = new TicketQueries(context, clock);

        // Add more tickets to test paging
        var customer1 = context.Set<Customer>().First(c => c.Name == "Contoso Ltd");
        var categoryTechnical = context.Set<Category>().First(c => c.Name == "Technical");
        var agent1 = context.Set<Agent>().First(a => a.FullName == "Alex Turner");

        var ticket4 = Ticket.Raise(
            "TCK-0045",
            "Another login issue",
            "Cannot login after password change",
            customer1.Id,
            categoryTechnical.Id,
            TicketPriority.High,
            clock.UtcNow.AddDays(-3)); // Older than TCK-0042 to ensure correct ordering

        ticket4.AssignTo(agent1.Id, clock.UtcNow);
        ticket4.ChangeStatus(TicketStatus.InProgress, clock.UtcNow);

        context.Set<Ticket>().Add(ticket4);
        await context.SaveChangesAsync();

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 1,
            PageSize = 1 // Page size of 1 to test paging
        };

        // Act
        var resultPage1 = await ticketQueries.GetPagedAsync(query, CancellationToken.None);

        // Assert page 1
        Assert.Equal(2, resultPage1.TotalCount); // Two high priority tickets
        Assert.Single(resultPage1.Items);
        Assert.Equal("TCK-0042", resultPage1.Items[0].Reference); // First ticket by CreatedAtUtc desc

        // Act page 2
        var queryPage2 = query with { Page = 2 };
        var resultPage2 = await ticketQueries.GetPagedAsync(queryPage2, CancellationToken.None);

        // Assert page 2
        Assert.Equal(2, resultPage2.TotalCount); // Same total count
        Assert.Single(resultPage2.Items);
        Assert.Equal("TCK-0045", resultPage2.Items[0].Reference); // Second ticket
    }
}