using System.Collections.Generic;
using System.Linq;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class AgentSelectorTests
{
    private readonly AgentSelector _selector;

    public AgentSelectorTests()
    {
        _selector = new AgentSelector();
    }

    [Fact]
    public void SelectAgent_WithEligibleAgents_SelectsFewestOpenTickets()
    {
        // Arrange
        var category = new Category("General", false, false);
        var agent1 = new Agent("Agent One", "agent1@test.com", 10, new DateTime(2026, 1, 1));
        var agent2 = new Agent("Agent Two", "agent2@test.com", 10, new DateTime(2026, 1, 1));
        var agent3 = new Agent("Agent Three", "agent3@test.com", 10, new DateTime(2026, 1, 1));
        var candidates = new List<Agent> { agent1, agent2, agent3 };

        // Act
        var (agentId, reason) = _selector.SelectAgent(candidates, category, agent =>
        {
            if (agent.FullName == "Agent One") return 5;
            if (agent.FullName == "Agent Two") return 3;
            if (agent.FullName == "Agent Three") return 7;
            return 0;
        });

        // Assert
        Assert.Equal(agent2.Id, agentId); // Agent Two has fewest open tickets (3)
        Assert.Contains("Selected agent with 3 open tickets", reason);
    }

    [Fact]
    public void SelectAgent_WithTieInOpenTickets_SelectsLowestAgentId()
    {
        // Arrange
        var category = new Category("General", false, false);
        var agent5 = new Agent("Agent Five", "agent5@test.com", 10, new DateTime(2026, 1, 1));
        var agent2 = new Agent("Agent Two", "agent2@test.com", 10, new DateTime(2026, 1, 1));
        var agent3 = new Agent("Agent Three", "agent3@test.com", 10, new DateTime(2026, 1, 1));
        var candidates = new List<Agent> { agent5, agent2, agent3 };

        // Act
        var (agentId, reason) = _selector.SelectAgent(candidates, category, agent =>
        {
            if (agent.FullName == "Agent Five") return 3;
            if (agent.FullName == "Agent Two") return 3;
            if (agent.FullName == "Agent Three") return 3;
            return 0;
        });

        // Assert
        Assert.Equal(agent2.Id, agentId); // Should pick Agent Two due to tie-break rule (lowest ID)
        Assert.Contains("Selected agent with 3 open tickets", reason);
    }

    [Fact]
    public void SelectAgent_WithSpecialistRequirement_OnlySelectsSpecializedAgents()
    {
        // Arrange
        var outageCategory = new Category("Outage", true, false); // Requires specialist
        var generalAgent = new Agent("General Agent", "agent1@test.com", 10, new DateTime(2026, 1, 1));
        var outageSpecialist = new Agent("Outage Specialist", "agent2@test.com", 10, new DateTime(2026, 1, 1));
        var anotherSpecialist = new Agent("Another Specialist", "agent3@test.com", 10, new DateTime(2026, 1, 1));
        var candidates = new List<Agent> { generalAgent, outageSpecialist, anotherSpecialist };

        // Set up specializations using the actual category Id
        generalAgent.AddSpecialization(1); // General category (different from Outage)
        outageSpecialist.AddSpecialization(outageCategory.Id); // Outage category
        anotherSpecialist.AddSpecialization(outageCategory.Id); // Outage category

        // Act
        var (agentId, reason) = _selector.SelectAgent(candidates, outageCategory, agent =>
        {
            if (agent.FullName == "General Agent") return 2;
            if (agent.FullName == "Outage Specialist") return 5;
            if (agent.FullName == "Another Specialist") return 1;
            return 0;
        });

        // Assert
        Assert.Equal(anotherSpecialist.Id, agentId); // Should pick Another Specialist (specialized in Outage with 1 open ticket, fewer than Outage Specialist's 5)
        Assert.Contains("Selected agent with 1 open tickets", reason);
    }

    [Fact]
    public void SelectAgent_WithAgentAtMaxCapacity_IsExcluded()
    {
        // Arrange
        var category = new Category("General", false, false);
        var atCapacityAgent = new Agent("At Capacity Agent", "agent1@test.com", 5, new DateTime(2026, 1, 1));
        var availableAgent = new Agent("Available Agent", "agent2@test.com", 5, new DateTime(2026, 1, 1));
        var candidates = new List<Agent> { atCapacityAgent, availableAgent };

        // Act
        var (agentId, reason) = _selector.SelectAgent(candidates, category, agent =>
        {
            if (agent.FullName == "At Capacity Agent") return 5;
            if (agent.FullName == "Available Agent") return 3;
            return 0;
        });

        // Assert
        Assert.Equal(availableAgent.Id, agentId); // Should pick Available Agent since At Capacity Agent is at max capacity
        Assert.Contains("Selected agent with 3 open tickets", reason);
    }

    [Fact]
    public void SelectAgent_WithInactiveAgent_IsExcluded()
    {
        // Arrange
        var category = new Category("General", false, false);
        var inactiveAgent = new Agent("Inactive Agent", "agent1@test.com", 10, new DateTime(2026, 1, 1));
        var activeAgent = new Agent("Active Agent", "agent2@test.com", 10, new DateTime(2026, 1, 1));
        var candidates = new List<Agent> { inactiveAgent, activeAgent };

        // Make the first agent inactive
        inactiveAgent.Deactivate();

        var openTicketCounts = new Dictionary<int, int>
        {
            [inactiveAgent.Id] = 2, // Inactive Agent: 2 open tickets
            [activeAgent.Id] = 5    // Active Agent: 5 open tickets
        };

        // Act
        var (agentId, reason) = _selector.SelectAgent(candidates, category, agent =>
        {
            if (agent.FullName == "Inactive Agent") return 2;
            if (agent.FullName == "Active Agent") return 5;
            return 0;
        });

        // Assert
        Assert.Equal(activeAgent.Id, agentId); // Should pick Active Agent since Inactive Agent is inactive
        Assert.Contains("Selected agent with 5 open tickets", reason);
    }

    [Fact]
    public void SelectAgent_WithNoEligibleAgents_ReturnsNullWithReason()
    {
        // Arrange
        var outageCategory = new Category("Outage", true, false); // Requires specialist
        var generalAgent = new Agent("General Agent", "agent1@test.com", 10, new DateTime(2026, 1, 1));
        var atCapacitySpecialist = new Agent("At Capacity Specialist", "agent2@test.com", 3, new DateTime(2026, 1, 1));
        var candidates = new List<Agent> { generalAgent, atCapacitySpecialist };

        // Set up specializations
        generalAgent.AddSpecialization(1); // General category (not Outage)
        atCapacitySpecialist.AddSpecialization(2); // Outage category

        // Act
        var (agentId, reason) = _selector.SelectAgent(candidates, outageCategory, agent =>
        {
            if (agent.FullName == "General Agent") return 2; // Not specialized in Outage
            if (agent.FullName == "At Capacity Specialist") return 3; // 3/3 open tickets (at max capacity)
            return 0;
        });

        // Assert
        Assert.Null(agentId);
        Assert.Equal("No eligible agents available", reason);
    }
}