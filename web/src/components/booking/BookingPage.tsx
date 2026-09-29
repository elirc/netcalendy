import { useState } from 'react';
import { useAvailableSlots } from '@/hooks/useAvailableSlots';
import { bookingsApi } from '@/api/bookings';
import { formatDate, formatTime, getUserTimezone } from '@/lib/utils';
import type { AvailableSlot } from '@/types';

interface BookingPageProps {
  eventTypeId: string;
  hostUserId: string;
  eventName: string;
  durationMinutes: number;
  color: string;
}

interface BookingForm {
  name: string;
  email: string;
  notes: string;
}

type Step = 'pick-slot' | 'fill-form' | 'confirmed';

export function BookingPage({ eventTypeId, hostUserId, eventName, durationMinutes, color }: BookingPageProps) {
  const [weekStart] = useState(() => {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d;
  });
  const [selected, setSelected] = useState<AvailableSlot | null>(null);
  const [step, setStep] = useState<Step>('pick-slot');
  const [form, setForm] = useState<BookingForm>({ name: '', email: '', notes: '' });
  const [cancellationToken, setCancellationToken] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const { data: slots = [], isLoading } = useAvailableSlots(eventTypeId, hostUserId, weekStart);

  const slotsByDay = slots.reduce<Record<string, AvailableSlot[]>>((acc, slot) => {
    const day = formatDate(slot.startUtc);
    if (!acc[day]) acc[day] = [];
    acc[day].push(slot);
    return acc;
  }, {});

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selected) return;
    setError('');
    setSubmitting(true);
    try {
      const booking = await bookingsApi.create({
        eventTypeId,
        hostUserId,
        startTime: selected.startUtc,
        inviteeName: form.name,
        inviteeEmail: form.email,
        inviteeTimezone: getUserTimezone(),
        notes: form.notes || undefined,
      });
      setCancellationToken(booking.cancellationToken);
      setStep('confirmed');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      setError(msg ?? 'Something went wrong. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  if (step === 'confirmed') {
    return (
      <div className="confirmed-screen">
        <div className="icon" style={{ color }}>✓</div>
        <h2>You're scheduled!</h2>
        <p>A calendar invitation has been sent to {form.email}.</p>
        <p className="small">Need to cancel?{' '}
          <a href={`/cancel/${cancellationToken}`}>Cancel this meeting</a>
        </p>
      </div>
    );
  }

  if (step === 'fill-form' && selected) {
    return (
      <div className="booking-form">
        <button className="back-btn" onClick={() => setStep('pick-slot')}>← Back</button>
        <div className="selected-slot" style={{ borderLeft: `4px solid ${color}` }}>
          <strong>{eventName}</strong>
          <span>{durationMinutes} min · {formatDate(selected.startUtc)} · {formatTime(selected.startUtc)}</span>
        </div>
        <form onSubmit={handleSubmit}>
          <label>
            Your name
            <input required value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} />
          </label>
          <label>
            Email address
            <input required type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} />
          </label>
          <label>
            Additional notes (optional)
            <textarea value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} rows={3} />
          </label>
          {error && <p className="error">{error}</p>}
          <button type="submit" style={{ background: color }} disabled={submitting}>
            {submitting ? 'Confirming…' : 'Confirm Booking'}
          </button>
        </form>
      </div>
    );
  }

  return (
    <div className="slot-picker">
      <div className="header" style={{ borderTop: `4px solid ${color}` }}>
        <h1>{eventName}</h1>
        <p>{durationMinutes} min meeting</p>
      </div>
      {isLoading ? (
        <p>Loading available times…</p>
      ) : Object.keys(slotsByDay).length === 0 ? (
        <p>No available times in the next 7 days.</p>
      ) : (
        Object.entries(slotsByDay).map(([day, daySlots]) => (
          <div key={day} className="day-group">
            <h3>{day}</h3>
            <div className="slots">
              {daySlots.map(slot => (
                <button
                  key={slot.startUtc}
                  className="slot-btn"
                  style={{ borderColor: color }}
                  onClick={() => { setSelected(slot); setStep('fill-form'); }}
                >
                  {formatTime(slot.startUtc)}
                </button>
              ))}
            </div>
          </div>
        ))
      )}
    </div>
  );
}
