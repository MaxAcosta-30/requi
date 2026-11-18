import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../environments/environment';
import { ApiStatusService, WAKE_MAX_RETRIES, WAKE_RETRY_DELAY_MS } from './api-status';

describe('ApiStatusService', () => {
  let service: ApiStatusService;
  let http: HttpTestingController;
  const url = `${environment.apiUrl}/health/live`;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ApiStatusService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('pasa a ready cuando la API contesta', () => {
    service.check();
    http.expectOne(url).flush('Healthy');
    expect(service.status()).toBe('ready');
  });

  it('muestra waking mientras la API despierta y luego ready', async () => {
    service.check();
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(service.status()).toBe('waking');

    await vi.advanceTimersByTimeAsync(WAKE_RETRY_DELAY_MS);
    http.expectOne(url).flush('Healthy');
    expect(service.status()).toBe('ready');
  });

  it('pasa a unavailable si se agotan los reintentos', async () => {
    service.check();
    for (let i = 0; i <= WAKE_MAX_RETRIES; i++) {
      http.expectOne(url).error(new ProgressEvent('error'));
      await vi.advanceTimersByTimeAsync(WAKE_RETRY_DELAY_MS);
    }
    expect(service.status()).toBe('unavailable');
  });
});
