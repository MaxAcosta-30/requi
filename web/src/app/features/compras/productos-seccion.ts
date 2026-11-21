import { Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';
import { DineroPipe } from '../../core/i18n/i18n';
import { ClaveError, ComprasRepositorio, claveError } from '../../repositories/compras';
import { ProductoAdmin, ProveedorAdmin } from '../../repositories/modelos';
import { Campo } from './campo';
import { Estado, EstadoCarga } from './estado';

/** El proveedor elegido debe estar entre los activos (la API también responde proveedor_invalido). */
export const proveedorActivo =
  (activos: () => string[]): ValidatorFn =>
  (c: AbstractControl): ValidationErrors | null =>
    !c.value || activos().includes(c.value) ? null : { inactivo: true };

/** Caso 2: productos. Filtro por proveedor, alta/edición y desactivación con confirmación en línea. */
@Component({
  selector: 'app-productos-seccion',
  host: { class: 'block' },
  imports: [ReactiveFormsModule, TranslocoPipe, DineroPipe, Campo, Estado],
  template: `
    <section aria-labelledby="t-productos" class="space-y-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 id="t-productos" class="text-2xl font-extrabold tracking-tight">{{ 'catalogo.productos' | transloco }}</h2>
        @if (sesion.tieneRol('Compras') && !editando()) {
          <button type="button" class="boton-primario" (click)="nuevo()">{{ 'catalogo.nuevoProducto' | transloco }}</button>
        }
      </div>

      @if (editando(); as e) {
        <form [formGroup]="form" (ngSubmit)="guardar()" class="hoja p-5 space-y-4 max-w-xl" novalidate>
          <h3 class="font-semibold">{{ (e === 'nuevo' ? 'catalogo.nuevoProducto' : 'catalogo.editarProducto') | transloco }}</h3>
          <app-campo idCampo="prod-proveedor" etiqueta="catalogo.proveedor" [control]="form.controls.proveedorId"
            [errores]="{ required: 'catalogo.errores.requerido', inactivo: 'catalogo.errores.proveedorInactivo' }">
            <select id="prod-proveedor" class="campo" formControlName="proveedorId"
              [attr.aria-invalid]="form.controls.proveedorId.invalid && form.controls.proveedorId.touched" aria-describedby="prod-proveedor-error">
              <option value="" disabled>{{ 'catalogo.elegirProveedor' | transloco }}</option>
              @for (p of activos(); track p.id) {
                <option [value]="p.id">{{ p.nombre }}</option>
              }
            </select>
          </app-campo>
          <app-campo idCampo="prod-sku" etiqueta="catalogo.sku" [control]="form.controls.sku"
            [errores]="{ required: 'catalogo.errores.requerido', pattern: 'catalogo.errores.sku' }">
            <input id="prod-sku" class="campo cifra" formControlName="sku" maxlength="40" autocomplete="off"
              [attr.aria-invalid]="form.controls.sku.invalid && form.controls.sku.touched" aria-describedby="prod-sku-error" />
          </app-campo>
          <app-campo idCampo="prod-nombre" etiqueta="catalogo.nombre" [control]="form.controls.nombre" [errores]="{ required: 'catalogo.errores.requerido' }">
            <input id="prod-nombre" class="campo" formControlName="nombre" maxlength="150" autocomplete="off"
              [attr.aria-invalid]="form.controls.nombre.invalid && form.controls.nombre.touched" aria-describedby="prod-nombre-error" />
          </app-campo>
          <div class="grid gap-4 sm:grid-cols-2">
            <app-campo idCampo="prod-unidad" etiqueta="catalogo.unidad" [control]="form.controls.unidad" [errores]="{ required: 'catalogo.errores.requerido' }">
              <input id="prod-unidad" class="campo" formControlName="unidad" maxlength="20" autocomplete="off"
                [attr.aria-invalid]="form.controls.unidad.invalid && form.controls.unidad.touched" aria-describedby="prod-unidad-error" />
            </app-campo>
            <app-campo idCampo="prod-precio" etiqueta="catalogo.precio" [control]="form.controls.precioUnitario"
              [errores]="{ required: 'catalogo.errores.precio', min: 'catalogo.errores.precio', pattern: 'catalogo.errores.precio' }">
              <input id="prod-precio" class="campo cifra" type="number" step="0.01" min="0" inputmode="decimal" formControlName="precioUnitario"
                [attr.aria-invalid]="form.controls.precioUnitario.invalid && form.controls.precioUnitario.touched" aria-describedby="prod-precio-error" />
            </app-campo>
          </div>
          @if (errorGuardar(); as k) {
            <p class="text-sm text-rojo" role="alert">{{ k | transloco }}</p>
          }
          <div class="flex flex-wrap gap-3">
            <button type="submit" class="boton-primario" [disabled]="guardando()">{{ 'catalogo.guardar' | transloco }}</button>
            <button type="button" class="boton-secundario" (click)="cancelar()">{{ 'catalogo.cancelar' | transloco }}</button>
          </div>
        </form>
      }

      @if (sesion.tieneRol('Compras')) {
        <div class="max-w-sm">
          <label class="block text-sm font-semibold mb-1" for="filtro-prov-prod">{{ 'catalogo.filtroProveedor' | transloco }}</label>
          <select id="filtro-prov-prod" class="campo" [value]="filtro()" (change)="filtrar($any($event.target).value)">
            <option value="">{{ 'catalogo.todosProveedores' | transloco }}</option>
            @for (p of proveedores(); track p.id) {
              <option [value]="p.id">{{ p.nombre }}</option>
            }
          </select>
        </div>
      }

      <app-estado [estado]="estado()" [vacio]="items().length === 0" vacioTitulo="catalogo.vacioProductos" vacioTexto="catalogo.vacioProductosTexto" (reintentar)="cargar()">
        @if (errorAccion(); as k) {
          <p class="text-sm text-rojo mb-2" role="alert">{{ k | transloco }}</p>
        }
        <ul class="hoja divide-y divide-renglon">
          @for (p of items(); track p.id) {
            <li class="p-4 grid gap-2 sm:grid-cols-[1fr_auto_auto] sm:items-center sm:gap-6">
              <div>
                <p class="font-semibold">
                  {{ p.nombre }}
                  @if (!p.activo) {
                    <span class="ml-2 rounded bg-gris px-2 py-0.5 text-xs font-semibold">{{ 'catalogo.inactivo' | transloco }}</span>
                  }
                </p>
                <p class="text-sm text-tinta-2"><span class="cifra">{{ p.sku }}</span> · {{ p.proveedor }} · {{ p.unidad }}</p>
              </div>
              <p class="cifra text-lg font-semibold text-carbon sm:text-right">{{ p.precioUnitario | dinero }}</p>
              <div class="flex flex-wrap items-center gap-2">
                @if (confirmando() === p.id) {
                  <span class="text-sm" role="alert">{{ 'catalogo.confirmarDesactivar' | transloco: { nombre: p.nombre } }}</span>
                  <button type="button" class="boton-primario" (click)="desactivar(p)">{{ 'catalogo.siDesactivar' | transloco }}</button>
                  <button type="button" class="boton-secundario" (click)="confirmando.set(null)">{{ 'catalogo.cancelar' | transloco }}</button>
                } @else {
                  <button type="button" class="boton-secundario" (click)="editar(p)">{{ 'catalogo.editar' | transloco }}<span class="sr-only"> {{ p.nombre }}</span></button>
                  @if (p.activo) {
                    <button type="button" class="boton-secundario" (click)="confirmando.set(p.id)">{{ 'catalogo.desactivar' | transloco }}<span class="sr-only"> {{ p.nombre }}</span></button>
                  }
                }
              </div>
            </li>
          }
        </ul>
      </app-estado>
    </section>
  `,
})
export class ProductosSeccion {
  protected readonly sesion = inject(SesionService);
  private readonly repo = inject(ComprasRepositorio);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly estado = signal<EstadoCarga>('cargando');
  protected readonly items = signal<ProductoAdmin[]>([]);
  protected readonly proveedores = signal<ProveedorAdmin[]>([]);
  protected readonly activos = computed(() => this.proveedores().filter((p) => p.activo));
  protected readonly filtro = signal('');
  protected readonly editando = signal<'nuevo' | ProductoAdmin | null>(null);
  protected readonly confirmando = signal<string | null>(null);
  protected readonly guardando = signal(false);
  protected readonly errorGuardar = signal<ClaveError | null>(null);
  protected readonly errorAccion = signal<ClaveError | null>(null);

  readonly form = this.fb.group({
    proveedorId: ['', [Validators.required, proveedorActivo(() => this.activos().map((p) => p.id))]],
    sku: ['', [Validators.required, Validators.maxLength(40), Validators.pattern(/^[A-Za-z0-9._-]+$/)]],
    nombre: ['', [Validators.required, Validators.maxLength(150)]],
    unidad: ['', [Validators.required, Validators.maxLength(20)]],
    precioUnitario: [null as number | null, [Validators.required, Validators.min(0.01), Validators.pattern(/^\d+(\.\d{1,2})?$/)]],
  });

  constructor() {
    if (this.sesion.tieneRol('Compras')) {
      void this.repo.proveedores().then((p) => this.proveedores.set(p)).catch(() => undefined);
    }
    void this.cargar();
  }

  async cargar(): Promise<void> {
    if (!this.sesion.tieneRol('Compras')) return;
    this.estado.set('cargando');
    try {
      this.items.set(await this.repo.productos({ proveedorId: this.filtro() || undefined }));
      this.estado.set('lista');
    } catch {
      this.estado.set('error');
    }
  }

  protected filtrar(id: string): void {
    this.filtro.set(id);
    void this.cargar();
  }

  protected nuevo(): void {
    this.form.reset({ proveedorId: this.filtro(), sku: '', nombre: '', unidad: '', precioUnitario: null });
    this.errorGuardar.set(null);
    this.editando.set('nuevo');
  }

  protected editar(p: ProductoAdmin): void {
    this.form.reset({ proveedorId: p.proveedorId, sku: p.sku, nombre: p.nombre, unidad: p.unidad, precioUnitario: p.precioUnitario });
    this.errorGuardar.set(null);
    this.editando.set(p);
  }

  protected cancelar(): void {
    this.editando.set(null);
  }

  async guardar(): Promise<void> {
    this.form.controls.proveedorId.updateValueAndValidity();
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const cuerpo = { proveedorId: v.proveedorId, sku: v.sku.trim(), nombre: v.nombre.trim(), unidad: v.unidad.trim(), precioUnitario: Number(v.precioUnitario) };
    const e = this.editando();
    this.guardando.set(true);
    this.errorGuardar.set(null);
    try {
      if (e === 'nuevo') await this.repo.crearProducto(cuerpo);
      else if (e) await this.repo.editarProducto(e.id, cuerpo);
      this.editando.set(null);
      await this.cargar();
    } catch (err) {
      this.errorGuardar.set(claveError(err));
    } finally {
      this.guardando.set(false);
    }
  }

  protected async desactivar(p: ProductoAdmin): Promise<void> {
    this.errorAccion.set(null);
    try {
      await this.repo.desactivarProducto(p.id);
      this.confirmando.set(null);
      await this.cargar();
    } catch (err) {
      this.confirmando.set(null);
      this.errorAccion.set(claveError(err));
    }
  }
}
