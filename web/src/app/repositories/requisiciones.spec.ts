import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideApiConfiguration } from '../api/api-configuration';
import { ErrorApi } from './modelos';
import { RequisicionesRepositorio } from './requisiciones';

const API = 'https://api.test';
const dto = {
  id: 'r1', folio: '143', estado: 'Submitted', proveedorId: 'p1', proveedor: 'Ferretería', solicitanteId: 'u1',
  total: '23180.00', notas: null, motivoRechazo: null, creadaEn: '2026-11-19T18:00:00Z', enviadaEn: '2026-11-19T18:00:00Z',
  firmasNecesarias: 2, firmas: [{ aprobadorId: 'a1', decision: 'Approved', motivo: null, fecha: '2026-11-19T19:00:00Z' }],
  renglones: [{ productoId: 'x', producto: 'Guantes', cantidad: 40, precioUnitario: '185.00', importe: '7400.00' }],
};

describe('RequisicionesRepositorio', () => {
  let repo: RequisicionesRepositorio;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration(API)] });
    repo = TestBed.inject(RequisicionesRepositorio);
    http = TestBed.inject(HttpTestingController);
  });

  it('lista por vista y convierte números y fechas', async () => {
    const promesa = repo.listar('por-aprobar');
    const req = await esperar(() => http.expectOne((r) => r.url === `${API}/api/requisiciones`));
    expect(req.request.params.get('vista')).toBe('por-aprobar');
    req.flush([dto]);
    const [r] = await promesa;
    expect(r.total).toBe(23180);
    expect(r.folio).toBe(143);
    expect(r.creadaEn).toBeInstanceOf(Date);
    expect(r.renglones[0].precioUnitario).toBe(185);
  });

  it('rechazar manda el motivo en el cuerpo', async () => {
    const promesa = repo.rechazar('r1', 'Sin presupuesto');
    const req = await esperar(() => http.expectOne(`${API}/api/requisiciones/r1/rechazar`));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ motivo: 'Sin presupuesto' });
    req.flush({ ...dto, estado: 'Rejected', motivoRechazo: 'Sin presupuesto' });
    expect((await promesa).estado).toBe('Rejected');
  });

  it('un 409 se convierte en ErrorApi con el código de la regla', async () => {
    const promesa = repo.aprobar('r1');
    const req = await esperar(() => http.expectOne(`${API}/api/requisiciones/r1/aprobar`));
    req.flush({ title: 'Regla de negocio', detail: 'Nadie aprueba lo suyo', codigo: 'propia' }, { status: 409, statusText: 'Conflict' });
    const error = await promesa.catch((e) => e);
    expect(error).toBeInstanceOf(ErrorApi);
    expect((error as ErrorApi).codigo).toBe('propia');
    expect((error as ErrorApi).estado).toBe(409);
  });
});

/** Api.invoke es asíncrono: la petición aparece un tick después. */
async function esperar<T>(fn: () => T): Promise<T> {
  for (let i = 0; i < 20; i++) {
    try {
      return fn();
    } catch {
      await new Promise((r) => setTimeout(r, 0));
    }
  }
  return fn();
}
