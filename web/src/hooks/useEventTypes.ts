import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { eventTypesApi } from '@/api/eventTypes';

export const EVENT_TYPES_KEY = ['event-types'] as const;

export function useEventTypes() {
  return useQuery({
    queryKey: EVENT_TYPES_KEY,
    queryFn: eventTypesApi.list,
  });
}

export function useDeleteEventType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: eventTypesApi.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: EVENT_TYPES_KEY }),
  });
}

export function useCreateEventType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: eventTypesApi.create,
    onSuccess: () => qc.invalidateQueries({ queryKey: EVENT_TYPES_KEY }),
  });
}
