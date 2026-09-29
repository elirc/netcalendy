import { useQuery } from '@tanstack/react-query';
import { bookingsApi } from '@/api/bookings';
import { format } from 'date-fns';

export function useAvailableSlots(eventTypeId: string, hostUserId: string, startDate: Date, daysAhead = 7) {
  const endDate = new Date(startDate);
  endDate.setDate(endDate.getDate() + daysAhead - 1);

  return useQuery({
    queryKey: ['slots', eventTypeId, hostUserId, format(startDate, 'yyyy-MM-dd')],
    queryFn: () => bookingsApi.getAvailableSlots({
      eventTypeId,
      hostUserId,
      startDate: format(startDate, 'yyyy-MM-dd'),
      endDate: format(endDate, 'yyyy-MM-dd'),
    }),
    enabled: Boolean(eventTypeId && hostUserId),
  });
}
