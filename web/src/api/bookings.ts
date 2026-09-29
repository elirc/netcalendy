import { apiClient } from './client';
import type { AvailableSlot, Booking, CreateBookingPayload } from '@/types';

export const bookingsApi = {
  getAvailableSlots: async (params: {
    eventTypeId: string;
    hostUserId: string;
    startDate: string;
    endDate: string;
  }): Promise<AvailableSlot[]> => {
    const { data } = await apiClient.get<AvailableSlot[]>('/schedules/available-slots', { params });
    return data;
  },

  create: async (payload: CreateBookingPayload): Promise<Booking> => {
    const { data } = await apiClient.post<Booking>('/bookings', payload);
    return data;
  },

  list: async (from?: string, to?: string): Promise<Booking[]> => {
    const { data } = await apiClient.get<Booking[]>('/bookings', { params: { from, to } });
    return data;
  },

  cancel: async (cancellationToken: string, reason?: string): Promise<void> => {
    await apiClient.post('/bookings/cancel', { cancellationToken, reason });
  },
};
