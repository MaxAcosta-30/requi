import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../environments/environment';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('avisa que la demo está despertando si la API no contesta', async () => {
    const fixture = TestBed.createComponent(App);
    TestBed.inject(HttpTestingController)
      .expectOne(`${environment.apiUrl}/health/live`)
      .error(new ProgressEvent('error'));
    await fixture.whenStable();

    const status = (fixture.nativeElement as HTMLElement).querySelector('[role=status]');
    expect(status?.textContent).toContain('Despertando la demo, puede tardar hasta un minuto.');
  });
});
