# Northwind Support Desk: candidate submission - Isuru Edirisinghe

## Status

| Task | State |
|---|---|
| 1.1 Defect fix | Complete, with regression tests |
| 1.2 Server-side filtering | Complete, with tests |
| 2 Escalation and assignment | **Backend complete, with unit tests. Frontend not done** (SLA indicator, SLA filter in the UI, escalation panel, history, triage result) |
| 3 Login and authentication | **Not started** |
| 4 Dockerise the API | **Not started** |

**Development login credentials: none.** Task 3 was not implemented, so the app and API have no login.

Run the project as described in the sections below (unchanged). Node 22 (`nvm use`) is required for the web tests.

## What was implemented

### Task 1.1: DEFECT-117 (filters sometimes do not take effect)
- **Root cause:** `useTicketList` fetched inside a `useEffect` whose dependency list was only `search`, `page` and `pageSize`. Changing status, priority, category, customer, agent, unassigned-only or sort updated state but never re-ran the effect, so no request was sent. Typing in search re-ran it and sent the current filters, which made the bug look like a stale value. An `eslint-disable` comment hid the lint warning that would have flagged it. The bug was masked when the page changed from 2+ to 1, because `page` is in the list and `updateFilters` resets it.
- **Fix:** the loader is now a `useCallback` keyed on `filters`, and the effect depends on that loader, the same pattern `useTicket` already uses. The lint suppression is removed.
- **Debounce:** TODO choose one. (a) All filters share the existing 300ms debounce, a known deviation from "immediate request" in the acceptance criteria. (b) Only search text is debounced and every other filter fetches immediately.
- **Regression test:** `useTicketList.test.ts` runs one case per filter (status, priority, category, customer, agent, unassigned-only, sort). Each starts on page 1, changes one filter and asserts a second request carrying the new value. It failed on the original code and passes on the fix.

### Task 1.2: Server-side filtering
- **Where:** `TicketQueries.ApplyFilters` in `SupportDesk.Infrastructure/Queries/TicketQueries.cs`. `GetPagedAsync` now does filter, then count, then sort, then skip/take.
- **Rules:** exact match for status, priority, category, customer and assigned agent. `unassignedOnly` means no agent. `search` is trimmed, ignored when blank, and matches title, reference or customer name with `Contains`. All filters combine with AND.
- **How the SQL was checked:** TODO describe what you did, e.g. EF command logging for one request, then paste the COUNT and the paged query (OFFSET/FETCH) here, and note that the WHERE appears in both.
- **Adding the next filter:** add one `if` block in `ApplyFilters`.
- **Tests:** `SearchTicketsQueryHandlerTests.cs` on in-memory SQLite: exact-match priority, combined filters, customer-name search (with padded spaces), unassigned-only, blank search, and counts and paging for a filtered set.

### Task 2: Escalation and assignment (backend)
Rules as I understood them:
- **Priority:** requested priority, default Medium; categories flagged `ForcesCriticalPriority` are always Critical.
- **SLA window:** Critical 4h, High 8h, Medium 24h, Low 72h. Premium customers get half, never below 1 hour. Windows, multiplier, floor and at-risk threshold come from the `Sla` configuration section, not code.
- **Assignment:** eligible agents are active, specialised where the category requires it, and strictly below `MaxOpenTickets`. Open tickets are counted from the tickets (not Resolved or Closed). Fewest open wins, ties go to the lowest agent id. If nobody is eligible the ticket is still created (201), unassigned, with a reason in the `triage` object.
- **Escalation:** `POST /api/tickets/{id}/escalate` raises priority exactly one level (the server decides). The due date is recomputed from the moment of escalation. The current agent is kept if still eligible, otherwise reassigned by the same selector. A reason of 5 to 500 characters is required. Each escalation writes an immutable history row, available from `GET /api/tickets/{id}/escalations` (newest first). Responses: 400 invalid reason or actor, 404 unknown ticket, 409 for Critical or Resolved/Closed with a message saying which.
- **SLA status:** derived, never stored, with an injectable clock. TODO state your boundary choices, e.g. what `now == due` gives and what exactly 25% remaining gives.
- **`slaStatus` filter:** one added block in `ApplyFilters`, done in SQL.
- **Not built:** all Task 2 UI.

## Design decisions

- **Rules live in small shared classes, not in handlers.** TODO list the real class names (window calculator, priority rule, agent selector, escalation rule, SLA evaluator). Create and Escalate call the same classes, so no rule exists twice, and they are tested with `FixedClock` without a database.
- **Current agent on escalation:** when deciding whether the current agent is still eligible, this ticket is not counted against their open total, otherwise an agent exactly at their limit would be reassigned for no reason. Chosen because the ticket is already theirs.
- **`Contains` instead of `EF.Functions.Like`:** EF handles `%` and `_` in user input safely.
- **Conflicting filters:** `assignedAgentId` together with `unassignedOnly=true` returns nothing, because both conditions apply.
- **SLA status is derived, not stored,** so it can never go stale when time passes. The cost is that the SQL form used by the filter must stay in step with `SlaEvaluator`.
- **Considered and rejected:** rules inside the command handlers (duplicated across create and escalate); one query per agent for open counts (N+1); filtering in C# after the query (breaks paging and counts).
- **Patterns used:** TODO confirm each matches your code. Repository and Unit of Work (existing), small policy/strategy-style rule classes, and the Options pattern for the `Sla` settings.

## Database

- Task 2 adds the `TicketEscalations` table through a committed EF migration: ticket id (indexed foreign key), from/to priority, agent and due date, reason (500), escalated-by (100) and timestamp.
- Index: TODO confirm. An index on `TicketId`, because history is always read per ticket.
- Reads are `AsNoTracking`, projected narrowly, and filtering happens in SQL before paging.

## Testing

- Run everything with `npm test` (the .NET tests, then the web tests). Web tests need Node 22.
- Covered: the 1.1 regression cases, the 1.2 filters and paging, and the Task 2 rules (TODO list: windows and floor, forced Critical, agent selection edge cases, escalation outcomes and rejections, SLA status boundaries).
- Deliberately not covered: Task 2 frontend tests (UI not built), and tests against real SQL Server. Query tests use SQLite, which matches `Contains` case-sensitively while SQL Server's default collation does not. I checked behaviour against the running SQL Server manually.

## AI usage

- **Tools:** Claude (chat) for orientation, debugging help, test design and reviewing output; Claude Code for implementing Task 2.
- **A workflow that worked:** a step-by-step plan where the agent had to stop after each step, show test output and wait for approval. Reviewing its output against the spec table caught problems early.
- **Accepted as-is:** TODO one concrete thing.
- **Rejected or changed:**
  - The first Claude Code attempt at Task 2 used `PATCH /escalation`, took the new priority from the caller, did not recompute the due date, did not reassign, and skipped triage on create. I rejected it and re-planned it step by step.
  - A later version, by its own log, assigned an Outage ticket to a non-specialist and a ticket to an agent already at their limit, and its escalated due date appeared to be counted from creation rather than from the escalation. These were treated as defects to fix with tests.
  - Chat advice first said a 300ms debounce on every filter was acceptable for 1.1. Re-reading the acceptance criteria ("immediate request") showed it was not, and it was corrected.
- **Bugs AI introduced or helped find:** the specialist and capacity assignment errors above; AI also helped locate the `useEffect` dependency defect.
- **How generated code was verified:** tests failing before the fix and passing after, generated SQL inspected, and manual checks through the UI, Swagger and curl on a freshly seeded database.

## Known limitations

- Tasks 2 (frontend), 3 and 4 are not done. With no authentication, anyone who can reach the API can read and write, and `escalatedBy` is a free-text value in the request body until Task 3 replaces it with the signed-in agent.
- Search uses a leading-wildcard `Contains`, which cannot use an index. At 10 million tickets I would use SQL Server full-text search or a dedicated search index.
- The SQL form of the `slaStatus` filter has to be kept in step with `SlaEvaluator` by hand.
- TODO remove if you shipped the search-only debounce: dropdown filters share the 300ms debounce, so they are not instantaneous.
- `dotnet` restore reports NU1902/NU1903 vulnerability warnings for `System.Security.Cryptography.Pkcs` 9.0.11 (already present before my changes). I did not change package versions.
