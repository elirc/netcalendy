import { useDeleteEventType } from '@/hooks/useEventTypes';
import type { EventType } from '@/types';

interface EventTypeCardProps {
  eventType: EventType;
}

export function EventTypeCard({ eventType }: EventTypeCardProps) {
  const { mutate: deleteType, isPending } = useDeleteEventType();

  return (
    <div className="event-type-card" style={{ borderLeft: `4px solid ${eventType.color}` }}>
      <div className="card-header">
        <div className="card-dot" style={{ background: eventType.color }} />
        <div>
          <h3>{eventType.name}</h3>
          <p>{eventType.durationMinutes} min</p>
          {eventType.description && <p className="description">{eventType.description}</p>}
        </div>
      </div>
      <div className="card-actions">
        <a href={`/book/${eventType.slug}`} className="link-btn">Copy link</a>
        <button
          className="danger-btn"
          onClick={() => deleteType(eventType.id)}
          disabled={isPending}
        >
          {isPending ? '…' : 'Delete'}
        </button>
      </div>
    </div>
  );
}
