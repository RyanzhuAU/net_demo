import { Injectable, signal } from '@angular/core';

export type NotificationLevel = 'success' | 'error' | 'info';

export interface Notification {
  readonly id: number;
  readonly level: NotificationLevel;
  readonly message: string;
  readonly correlationId: string | null;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextId = 1;
  private readonly items = signal<readonly Notification[]>([]);

  readonly notifications = this.items.asReadonly();

  success(message: string): void {
    this.push('success', message, null);
  }

  info(message: string): void {
    this.push('info', message, null);
  }

  error(message: string, correlationId: string | null = null): void {
    this.push('error', message, correlationId);
  }

  dismiss(id: number): void {
    this.items.update((current) => current.filter((n) => n.id !== id));
  }

  clear(): void {
    this.items.set([]);
  }

  private push(level: NotificationLevel, message: string, correlationId: string | null): void {
    const notification: Notification = { id: this.nextId++, level, message, correlationId };
    this.items.update((current) => [...current, notification]);
  }
}
