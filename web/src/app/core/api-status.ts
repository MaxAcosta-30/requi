import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { retry, timeout } from 'rxjs';
import { environment } from '../../environments/environment';

export type ApiStatus = 'checking' | 'waking' | 'ready' | 'unavailable';

/** Reintentos cada 5 s durante ~90 s: Render Free tarda hasta un minuto en despertar. */
export const WAKE_RETRY_DELAY_MS = 5_000;
export const WAKE_MAX_RETRIES = 18;

@Injectable({ providedIn: 'root' })
export class ApiStatusService {
  private readonly http = inject(HttpClient);
  readonly status = signal<ApiStatus>('checking');

  check(): void {
    this.status.set('checking');
    this.http
      .get(`${environment.apiUrl}/health/live`, { responseType: 'text' })
      .pipe(
        timeout(10_000),
        retry({
          count: WAKE_MAX_RETRIES,
          delay: () => {
            this.status.set('waking');
            return new Promise((resolve) => setTimeout(resolve, WAKE_RETRY_DELAY_MS));
          },
        }),
      )
      .subscribe({
        next: () => this.status.set('ready'),
        error: () => this.status.set('unavailable'),
      });
  }
}
