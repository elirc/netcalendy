export interface EventType {
  id: string;
  ownerId: string;
  name: string;
  slug: string;
  durationMinutes: number;
  description?: string;
  color: string;
  isActive: boolean;
}

export interface AvailableSlot {
  startUtc: string;
  endUtc: string;
  displayTime: string;
}

export interface Booking {
  id: string;
  eventTypeId: string;
  hostId: string;
  inviteeName: string;
  inviteeEmail: string;
  inviteeTimezone: string;
  inviteeNotes?: string;
  startTime: string;
  endTime: string;
  status: 'Pending' | 'Confirmed' | 'Cancelled' | 'NoShow';
  cancellationToken: string;
  cancellationReason?: string;
}

export interface CreateBookingPayload {
  eventTypeId: string;
  hostUserId: string;
  startTime: string;
  inviteeName: string;
  inviteeEmail: string;
  inviteeTimezone: string;
  notes?: string;
}
