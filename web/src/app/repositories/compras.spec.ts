import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideApiConfiguration } from '../api/api-configuration';
import { ComprasRepositorio, claveError } from './compras';
import { ErrorApi } from './modelos';

const API = 'https://api.test';

describe('ComprasRepositorio', () => {
  let repo: ComprasRepositorio;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration(API)] });
    repo = TestBed.inject(ComprasRepositorio);
    http = TestBed.inject(HttpTestingController);
  });

  it('lista productos por proveedor y convierte precio a número', async () => {
    const promesa = repo.productos({ proveedorId: 'p1' });
    const req = await esperar(() => http.expectOne((r) => r.url === `${API}/api/productos`));
    expect(req.request.params.get('proveedorId')).toBe('p1');
    req.flush([{ id: 'x', proveedorId: 'p1', proveedor: 'Ferretería', sku: 'GU-1', nombre: 'Guantes', unidad: 'par', precioUnitario: '185.50', activo: false }]);
    const [p] = await promesa;
    expect(p.precioUnitario).toBe(185.5);
    expect(p.activo).toBe(false);
  });

  it('lista órdenes con folio, total y fecha convertidos', async () => {
    const promesa = repo.ordenes();
    const req = await esperar(() => http.expectOne(`${API}/api/ordenes`));
    req.flush([{ id: 'o1', folio: '7', requisicionId: 'r1', proveedorId: 'p1', proveedor: 'Ferretería', total: '23180.00', creadaEn: '2026-11-19T18:00:00Z' }]);
    const [o] = await promesa;
    expect([o.folio, o.total]).toEqual([7, 23180]);
    expect(o.creadaEn).toBeInstanceOf(Date);
  });

  it('lista mensajes con el filtro de estado', async () => {
    const promesa = repo.mensajes('Failed');
    const req = await esperar(() => http.expectOne((r) => r.url === `${API}/api/outbox`));
    expect(req.request.params.get('estado')).toBe('Failed');
    req.flush([{ id: 'm1', tipo: 'OrdenGenerada', estado: 'Failed', intentos: 5, ultimoError: 'boom', ocurrioEn: '2026-11-19T18:00:00Z', procesadoEn: null }]);
    const [m] = await promesa;
    expect(m.intentos).toBe(5);
    expect(m.procesadoEn).toBeNull();
  });

  it('crear producto manda el cuerpo y un 409 duplicado se mapea a su clave', async () => {
    const entrada = { proveedorId: 'p1', sku: 'GU-1', nombre: 'Guantes', unidad: 'par', precioUnitario: 10 };
    const promesa = repo.crearProducto(entrada);
    const req = await esperar(() => http.expectOne(`${API}/api/productos`));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(entrada);
    req.flush({ title: 'Ya existe', detail: 'Ya existe un registro', codigo: 'duplicado' }, { status: 409, statusText: 'Conflict' });
    const error = await promesa.catch((e) => e);
    expect(error).toBeInstanceOf(ErrorApi);
    expect(claveError(error)).toBe('errores.duplicado');
  });

  it('desactivar proveedor hace POST y resuelve en 204', async () => {
    const promesa = repo.desactivarProveedor('p1');
    const req = await esperar(() => http.expectOne(`${API}/api/proveedores/p1/desactivar`));
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    await expect(promesa).resolves.toBeUndefined();
  });
});

describe('claveError', () => {
  it('mapea los códigos 409, 403 y 404; lo desconocido va a otro', () => {
    expect(claveError(new ErrorApi(409, 'proveedor_invalido', ''))).toBe('errores.proveedor_invalido');
    expect(claveError(new ErrorApi(409, 'referencia', ''))).toBe('errores.referencia');
    expect(claveError(new ErrorApi(403, 'red', ''))).toBe('errores.permisos');
    expect(claveError(new ErrorApi(404, 'red', ''))).toBe('errores.no_encontrado');
    expect(claveError(new ErrorApi(409, 'inventado', ''))).toBe('errores.otro');
    expect(claveError(new Error('x'))).toBe('errores.otro');
  });
});

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
