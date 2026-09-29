import { apiClient } from './client';
import type { EventType } from '@/types';

export const eventTypesApi = {
  list: async (): Promise<EventType[]> => {
    const { data } = await apiClient.get<EventType[]>('/event-types');
    return data;
  },

  get: async (id: string): Promise<EventType> => {
    const { data } = await apiClient.get<EventType>(`/event-types/${id}`);
    return data;
  },

  create: async (payload: {
    name: string;
    slug: string;
    durationMinutes: number;
    description?: string;
    color?: string;
  }): Promise<EventType> => {
    const { data } = await apiClient.post<EventType>('/event-types', payload);
    return data;
  },

  update: async (id: string, payload: { name: string; durationMinutes: number; description?: string; color: string }): Promise<void> => {
    await apiClient.put(`/event-types/${id}`, payload);
  },

  delete: async (id: string): Promise<void> => {
    await apiClient.delete(`/event-types/${id}`);
  },
};
