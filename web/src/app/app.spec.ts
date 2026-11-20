import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../environments/environment';
import { App } from './app';

function oidcFalso(sesion: { isAuthenticated: boolean; userData: unknown }) {
  return {
    checkAuth: vi.fn(() => of(sesion)),
    authorize: vi.fn(),
    logoff: vi.fn(() => of(null)),
  };
}

async function crear(oidc: ReturnType<typeof oidcFalso>) {
  await TestBed.configureTestingModule({
    imports: [App],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([]),
      { provide: OidcSecurityService, useValue: oidc },
    ],
  }).compileComponents();
  const fixture = TestBed.createComponent(App);
  const http = TestBed.inject(HttpTestingController);
  return { fixture, http, html: () => fixture.nativeElement as HTMLElement };
}

describe('App', () => {
  let oidc: ReturnType<typeof oidcFalso>;

  beforeEach(() => {
    oidc = oidcFalso({ isAuthenticated: false, userData: null });
  });

  it('avisa que la demo está despertando si la API no contesta', async () => {
    const { fixture, http, html } = await crear(oidc);
    http.expectOne(`${environment.apiUrl}/health/live`).error(new ProgressEvent('error'));
    await fixture.whenStable();

    expect(html().querySelector('[role=status]')?.textContent).toContain(
      'Despertando la demo, puede tardar hasta un minuto.',
    );
  });

  it('no toca OIDC mientras la API no responde', async () => {
    const { fixture, http } = await crear(oidc);
    http.expectOne(`${environment.apiUrl}/health/live`).error(new ProgressEvent('error'));
    await fixture.whenStable();

    expect(oidc.checkAuth).not.toHaveBeenCalled();
  });

  it('cuando la API responde revisa la sesión y ofrece iniciar sesión', async () => {
    const { fixture, http, html } = await crear(oidc);
    http.expectOne(`${environment.apiUrl}/health/live`).flush('Healthy');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(oidc.checkAuth).toHaveBeenCalledTimes(1);
    const boton = [...html().querySelectorAll('button')].find((b) => b.textContent?.includes('Iniciar sesión'));
    boton!.click();
    expect(oidc.authorize).toHaveBeenCalledTimes(1);
  });

  it('con sesión muestra al usuario y su rol', async () => {
    oidc = oidcFalso({
      isAuthenticated: true,
      userData: { sub: '1', name: 'Ana Pérez', email: 'ana@requi.test', role: 'Aprobador' },
    });
    const { fixture, http, html } = await crear(oidc);
    http.expectOne(`${environment.apiUrl}/health/live`).flush('Healthy');
    await fixture.whenStable();
    fixture.detectChanges();

    const texto = html().querySelector('[aria-label=Sesión]')?.textContent ?? '';
    expect(texto).toContain('Ana Pérez');
    expect(texto).toContain('Aprobador');
    expect(texto).toContain('Cerrar sesión');
  });
});
