import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import en from '../../../../public/i18n/en.json';
import es from '../../../../public/i18n/es.json';
import { SesionService } from '../../core/auth/sesion';
import { ComprasRepositorio } from '../../repositories/compras';
import { ProveedorAdmin } from '../../repositories/modelos';
import { ProductosSeccion } from './productos-seccion';

const proveedores: ProveedorAdmin[] = [
  { id: 'p1', nombre: 'Ferretería', correo: 'a@b.mx', telefono: null, activo: true },
  { id: 'p2', nombre: 'Vieja', correo: 'v@b.mx', telefono: null, activo: false },
];

async function crear() {
  const repo = {
    proveedores: vi.fn(async () => proveedores),
    productos: vi.fn(async () => []),
    crearProducto: vi.fn(async () => ({})),
    editarProducto: vi.fn(),
    desactivarProducto: vi.fn(),
  };
  await TestBed.configureTestingModule({
    imports: [TranslocoTestingModule.forRoot({ langs: { es, en }, translocoConfig: { availableLangs: ['es', 'en'], defaultLang: 'es' } })],
    providers: [
      { provide: ComprasRepositorio, useValue: repo },
      { provide: SesionService, useValue: { tieneRol: () => true } },
    ],
  }).compileComponents();
  const fixture = TestBed.createComponent(ProductosSeccion);
  await fixture.whenStable();
  return { repo, comp: fixture.componentInstance };
}

const valido = { proveedorId: 'p1', sku: 'GU-1', nombre: 'Guantes', unidad: 'par', precioUnitario: 185.5 };

describe('formulario de producto', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('campos requeridos, SKU, precio y proveedor inactivo se validan', async () => {
    const { comp } = await crear();
    const f = comp.form;
    expect(f.invalid).toBe(true);
    f.patchValue(valido);
    expect(f.valid).toBe(true);

    f.patchValue({ sku: 'con espacio' });
    expect(f.controls.sku.hasError('pattern')).toBe(true);
    f.patchValue({ sku: 'GU-1', precioUnitario: 0 });
    expect(f.controls.precioUnitario.invalid).toBe(true);
    f.patchValue({ precioUnitario: 1.234 });
    expect(f.controls.precioUnitario.invalid).toBe(true);
    f.patchValue({ precioUnitario: 10, unidad: '' });
    expect(f.controls.unidad.hasError('required')).toBe(true);
    f.patchValue({ unidad: 'par', proveedorId: 'p2' });
    expect(f.controls.proveedorId.hasError('inactivo')).toBe(true);
  });

  it('no envía si es inválido y envía el cuerpo si es válido', async () => {
    const { comp, repo } = await crear();
    (comp as unknown as { editando: { set(v: string): void } }).editando.set('nuevo');
    await comp.guardar();
    expect(repo.crearProducto).not.toHaveBeenCalled();
    expect(comp.form.controls.sku.touched).toBe(true);

    comp.form.patchValue(valido);
    await comp.guardar();
    expect(repo.crearProducto).toHaveBeenCalledWith(valido);
  });
});
