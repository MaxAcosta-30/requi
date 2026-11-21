import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';
import { ClaveError, ComprasRepositorio, claveError } from '../../repositories/compras';
import { ProveedorAdmin } from '../../repositories/modelos';
import { Campo } from './campo';
import { Estado, EstadoCarga } from './estado';

/** Caso 2: proveedores. Lista con inactivos, alta/edición y desactivación con confirmación en línea. */
@Component({
  selector: 'app-proveedores-seccion',
  host: { class: 'block' },
  imports: [ReactiveFormsModule, TranslocoPipe, Campo, Estado],
  template: `
    <section aria-labelledby="t-proveedores" class="space-y-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 id="t-proveedores" class="text-2xl font-extrabold tracking-tight">{{ 'catalogo.proveedores' | transloco }}</h2>
        @if (sesion.tieneRol('Compras') && !editando()) {
          <button type="button" class="boton-primario" (click)="nuevo()">{{ 'catalogo.nuevoProveedor' | transloco }}</button>
        }
      </div>

      @if (editando(); as e) {
        <form [formGroup]="form" (ngSubmit)="guardar()" class="hoja p-5 space-y-4 max-w-xl" novalidate>
          <h3 class="font-semibold">{{ (e === 'nuevo' ? 'catalogo.nuevoProveedor' : 'catalogo.editarProveedor') | transloco }}</h3>
          <app-campo idCampo="prov-nombre" etiqueta="catalogo.nombre" [control]="form.controls.nombre" [errores]="{ required: 'catalogo.errores.requerido' }">
            <input id="prov-nombre" class="campo" formControlName="nombre" maxlength="150" autocomplete="off"
              [attr.aria-invalid]="form.controls.nombre.invalid && form.controls.nombre.touched" aria-describedby="prov-nombre-error" />
          </app-campo>
          <app-campo idCampo="prov-correo" etiqueta="catalogo.correo" [control]="form.controls.correo"
            [errores]="{ required: 'catalogo.errores.requerido', email: 'catalogo.errores.correo' }">
            <input id="prov-correo" class="campo" type="email" formControlName="correo" maxlength="254" autocomplete="off"
              [attr.aria-invalid]="form.controls.correo.invalid && form.controls.correo.touched" aria-describedby="prov-correo-error" />
          </app-campo>
          <app-campo idCampo="prov-telefono" etiqueta="catalogo.telefono" [control]="form.controls.telefono" [errores]="{ pattern: 'catalogo.errores.telefono' }">
            <input id="prov-telefono" class="campo" type="tel" formControlName="telefono" maxlength="30" autocomplete="off"
              [attr.aria-invalid]="form.controls.telefono.invalid && form.controls.telefono.touched" aria-describedby="prov-telefono-error" />
          </app-campo>
          @if (errorGuardar(); as k) {
            <p class="text-sm text-rojo" role="alert">{{ k | transloco }}</p>
          }
          <div class="flex flex-wrap gap-3">
            <button type="submit" class="boton-primario" [disabled]="guardando()">{{ 'catalogo.guardar' | transloco }}</button>
            <button type="button" class="boton-secundario" (click)="cancelar()">{{ 'catalogo.cancelar' | transloco }}</button>
          </div>
        </form>
      }

      <app-estado [estado]="estado()" [vacio]="items().length === 0" vacioTitulo="catalogo.vacioProveedores" vacioTexto="catalogo.vacioProveedoresTexto" (reintentar)="cargar()">
        @if (errorAccion(); as k) {
          <p class="text-sm text-rojo mb-2" role="alert">{{ k | transloco }}</p>
        }
        <ul class="hoja divide-y divide-renglon">
          @for (p of items(); track p.id) {
            <li class="p-4 grid gap-2 sm:grid-cols-[1fr_auto] sm:items-center">
              <div>
                <p class="font-semibold">
                  {{ p.nombre }}
                  @if (!p.activo) {
                    <span class="ml-2 rounded bg-gris px-2 py-0.5 text-xs font-semibold">{{ 'catalogo.inactivo' | transloco }}</span>
                  }
                </p>
                <p class="text-sm text-tinta-2 break-all">{{ p.correo }}@if (p.telefono) { · <span class="cifra">{{ p.telefono }}</span> }</p>
              </div>
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
export class ProveedoresSeccion {
  protected readonly sesion = inject(SesionService);
  private readonly repo = inject(ComprasRepositorio);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly estado = signal<EstadoCarga>('cargando');
  protected readonly items = signal<ProveedorAdmin[]>([]);
  protected readonly editando = signal<'nuevo' | ProveedorAdmin | null>(null);
  protected readonly confirmando = signal<string | null>(null);
  protected readonly guardando = signal(false);
  protected readonly errorGuardar = signal<ClaveError | null>(null);
  protected readonly errorAccion = signal<ClaveError | null>(null);

  readonly form = this.fb.group({
    nombre: ['', [Validators.required, Validators.maxLength(150)]],
    correo: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    telefono: ['', [Validators.maxLength(30), Validators.pattern(/^[0-9 +()-]*$/)]],
  });

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    if (!this.sesion.tieneRol('Compras')) return;
    this.estado.set('cargando');
    try {
      this.items.set(await this.repo.proveedores());
      this.estado.set('lista');
    } catch {
      this.estado.set('error');
    }
  }

  protected nuevo(): void {
    this.form.reset({ nombre: '', correo: '', telefono: '' });
    this.errorGuardar.set(null);
    this.editando.set('nuevo');
  }

  protected editar(p: ProveedorAdmin): void {
    this.form.reset({ nombre: p.nombre, correo: p.correo, telefono: p.telefono ?? '' });
    this.errorGuardar.set(null);
    this.editando.set(p);
  }

  protected cancelar(): void {
    this.editando.set(null);
  }

  async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const cuerpo = { nombre: v.nombre.trim(), correo: v.correo.trim(), telefono: v.telefono.trim() || null };
    const e = this.editando();
    this.guardando.set(true);
    this.errorGuardar.set(null);
    try {
      if (e === 'nuevo') await this.repo.crearProveedor(cuerpo);
      else if (e) await this.repo.editarProveedor(e.id, cuerpo);
      this.editando.set(null);
      await this.cargar();
    } catch (err) {
      this.errorGuardar.set(claveError(err));
    } finally {
      this.guardando.set(false);
    }
  }

  protected async desactivar(p: ProveedorAdmin): Promise<void> {
    this.errorAccion.set(null);
    try {
      await this.repo.desactivarProveedor(p.id);
      this.confirmando.set(null);
      await this.cargar();
    } catch (err) {
      this.confirmando.set(null);
      this.errorAccion.set(claveError(err));
    }
  }
}
