import { useState } from 'react';
import { useEventTypes, useCreateEventType } from '@/hooks/useEventTypes';
import { EventTypeCard } from '@/components/dashboard/EventTypeCard';
import { slugify } from '@/lib/utils';

export default function DashboardPage() {
  const { data: eventTypes = [], isLoading, isError } = useEventTypes();
  const { mutate: createEventType, isPending } = useCreateEventType();
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState('');
  const [duration, setDuration] = useState(30);

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    createEventType(
      { name, slug: slugify(name), durationMinutes: duration },
      { onSuccess: () => { setShowForm(false); setName(''); setDuration(30); } }
    );
  };

  if (isLoading) return <p>Loading…</p>;
  if (isError) return <p>Failed to load event types.</p>;

  return (
    <div className="dashboard">
      <div className="page-header">
        <h1>Event Types</h1>
        <button className="primary-btn" onClick={() => setShowForm(true)}>+ New Event Type</button>
      </div>

      {showForm && (
        <form className="create-form" onSubmit={handleCreate}>
          <h2>Create Event Type</h2>
          <label>
            Name
            <input required value={name} onChange={e => setName(e.target.value)} placeholder="30 Minute Meeting" />
          </label>
          <label>
            Duration (minutes)
            <select value={duration} onChange={e => setDuration(Number(e.target.value))}>
              {[15, 30, 45, 60, 90, 120].map(d => <option key={d} value={d}>{d} min</option>)}
            </select>
          </label>
          <div className="form-actions">
            <button type="button" onClick={() => setShowForm(false)}>Cancel</button>
            <button type="submit" disabled={isPending}>
              {isPending ? 'Creating…' : 'Create'}
            </button>
          </div>
        </form>
      )}

      {eventTypes.length === 0 ? (
        <div className="empty-state">
          <p>No event types yet. Create one to start accepting bookings.</p>
        </div>
      ) : (
        <div className="card-grid">
          {eventTypes.map(et => <EventTypeCard key={et.id} eventType={et} />)}
        </div>
      )}
    </div>
  );
}
