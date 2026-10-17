import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';
import type { TicketFilters } from '../../../types/api';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
    });

      // Every filter the user can change must trigger a new request.
  const filterChanges: Array<[string, Partial<TicketFilters>]> = [
    ['status', { status: 'Open' }],
    ['priority', { priority: 'High' }],
    ['category', { categoryId: 3 }],
    ['customer', { customerId: 4 }],
    ['assigned agent', { assignedAgentId: 5 }],
    ['unassigned only', { unassignedOnly: true }],
    ['sort order', { sortBy: 'priority', sortDirection: 'desc' }],
  ];

  it.each(filterChanges)('fetches again when the %s filter changes', async (_name, patch) => {
    const { result } = renderHook(() => useTicketList());

    // Initial load (we are on page 1).
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    expect(getTickets).toHaveBeenCalledTimes(1);

    // The user changes a filter.
    act(() => {
      result.current.updateFilters(patch);
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    // A new request was sent, carrying the new value.
    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining(patch));
  });
});
