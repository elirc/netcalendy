import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { eventTypesApi } from '@/api/eventTypes';
import { BookingPage } from '@/components/booking/BookingPage';

export default function BookPage() {
  const { hostSlug, eventSlug } = useParams<{ hostSlug: string; eventSlug: string }>();

  // In a real app, you'd resolve host + eventType from slugs via a public endpoint.
  // For now we fetch by event type ID passed as the slug for simplicity.
  const { data: eventType, isLoading, isError } = useQuery({
    queryKey: ['public-event-type', eventSlug],
    queryFn: () => eventTypesApi.get(eventSlug!),
    enabled: Boolean(eventSlug),
  });

  if (isLoading) return <div className="loading">Loading…</div>;
  if (isError || !eventType) return <div className="error-page">Event type not found.</div>;

  return (
    <BookingPage
      eventTypeId={eventType.id}
      hostUserId={eventType.ownerId}
      eventName={eventType.name}
      durationMinutes={eventType.durationMinutes}
      color={eventType.color}
    />
  );
}
