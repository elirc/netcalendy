import { clsx, type ClassValue } from 'clsx';
import { format, parseISO } from 'date-fns';

export function cn(...inputs: ClassValue[]) {
  return clsx(inputs);
}

export function formatDateTime(iso: string): string {
  return format(parseISO(iso), 'PPP p');
}

export function formatTime(iso: string): string {
  return format(parseISO(iso), 'p');
}

export function formatDate(iso: string): string {
  return format(parseISO(iso), 'EEEE, MMMM d');
}

export function getUserTimezone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

export function slugify(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}
