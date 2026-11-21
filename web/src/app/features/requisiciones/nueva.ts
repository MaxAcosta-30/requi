import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { DineroPipe } from '../../core/i18n/i18n';
import { CatalogoRepositorio } from '../../repositories/catalogo';
import { ErrorApi, LIMITE_UNA_FIRMA, Producto, Proveedor } from '../../repositories/modelos';
import { RequisicionesRepositorio } from '../../repositories/requisiciones';

export type FormRenglon = FormGroup<{
  productoId: import('@angular/forms').FormControl<string>;
  cantidad: import('@angular/forms').FormControl<number>;
}>;

/** Un producto no puede repetirse en dos renglones (la API también lo rechaza). */
export const sinProductosRepetidos: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const ids = (control as FormArray<FormRenglon>).controls.map((c) => c.controls.productoId.value).filter(Boolean);
  return new Set(ids).size === ids.length ? null : { repetido: true };
};

/** Caso 3: renglones dinámicos con FormArray; el total final lo calcula el servidor. */
@Component({
  selector: 'app-nueva',
  imports: [ReactiveFormsModule, TranslocoPipe, DineroPipe],
  template: `
    <form [formGroup]="form" (ngSubmit)="guardar(true)" class="hoja max-w-3xl p-6 sm:p-8 space-y-6" novalidate>
      <div class="flex flex-wrap items-baseline justify-between gap-2 border-b border-renglon pb-4">
        <h1 class="text-2xl font-extrabold tracking-tight">{{ 'nueva.titulo' | transloco }}</h1>
        <p class="cifra text-sm text-tinta-3">{{ 'nueva.folioAlEnviar' | transloco }}</p>
      </div>

      <label class="block space-y-1">
        <span class="text-sm font-semibold">{{ 'nueva.proveedor' | transloco }}</span>
        <select class="campo" formControlName="proveedorId" (change)="cambiarProveedor()">
          <option value="" disabled>{{ 'nueva.elige' | transloco }}</option>
          @for (p of proveedores(); track p.id) {
            <option [value]="p.id">{{ p.nombre }}</option>
          }
        </select>
      </label>

      <fieldset class="space-y-2" formArrayName="renglones">
        <legend class="text-sm font-semibold mb-2">{{ 'nueva.renglones' | transloco }}</legend>
        @for (r of renglones.controls; track r; let i = $index) {
          <div class="grid grid-cols-[1fr_auto] sm:grid-cols-[1fr_6rem_8rem_8rem_2.75rem] gap-3 items-start border-b border-renglon pb-3" [formGroupName]="i">
            <select class="campo col-span-2 sm:col-span-1" formControlName="productoId" [attr.aria-label]="('nueva.producto' | transloco) + ' ' + (i + 1)">
              <option value="" disabled>{{ 'nueva.producto' | transloco }}</option>
              @for (p of productos(); track p.id) {
                <option [value]="p.id">{{ p.nombre }}</option>
              }
            </select>
            <div>
              <input class="campo cifra" type="number" min="1" step="1" inputmode="numeric" formControlName="cantidad"
                [attr.aria-label]="('nueva.cantidad' | transloco) + ' ' + (i + 1)"
                [attr.aria-invalid]="invalido(r, 'cantidad')" [attr.aria-describedby]="invalido(r, 'cantidad') ? 'error-cant-' + i : null"
                [class.ring-2]="invalido(r, 'cantidad')" [class.ring-rojo]="invalido(r, 'cantidad')" />
              @if (invalido(r, 'cantidad')) {
                <p class="text-xs text-rojo mt-1" [id]="'error-cant-' + i">{{ 'nueva.errores.cantidad' | transloco }}</p>
              }
            </div>
            <span class="cifra text-right text-tinta-2 pt-3">{{ precio(r) | dinero }}</span>
            <span class="cifra text-right font-semibold text-carbon pt-3">{{ importe(r) | dinero }}</span>
            <button type="button" class="boton px-0 text-tinta-3 hover:text-rojo" (click)="quitar(i)" [disabled]="renglones.length === 1"
              [attr.aria-label]="'nueva.quitar' | transloco: { n: i + 1 }">
              <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>
            </button>
          </div>
        }
        @if (renglones.hasError('repetido')) {
          <p class="text-sm text-rojo" role="alert">{{ 'nueva.errores.repetido' | transloco }}</p>
        }
        <button type="button" class="boton-secundario mt-1" (click)="agregar()">{{ 'nueva.agregar' | transloco }}</button>
      </fieldset>

      <div class="flex flex-wrap items-end justify-between gap-4 rounded bg-mesa p-4">
        <div>
          <p class="text-xs font-semibold text-tinta-3">{{ 'nueva.total' | transloco }}</p>
          <p class="cifra text-3xl font-semibold text-carbon tracking-tight">{{ total() | dinero }}</p>
          <p class="text-xs text-tinta-3 mt-1">{{ 'nueva.totalNota' | transloco }}</p>
        </div>
        <div class="text-sm text-right">
          <p class="font-semibold">{{ (total() > limite ? 'nueva.firmas2' : 'nueva.firmas1') | transloco }}</p>
          <p class="text-tinta-3">{{ 'nueva.firmasNota' | transloco }}</p>
        </div>
      </div>

      <label class="block space-y-1">
        <span class="text-sm font-semibold">{{ 'nueva.notas' | transloco }} <span class="font-normal text-tinta-3">{{ 'nueva.opcional' | transloco }}</span></span>
        <textarea class="campo py-2 min-h-20" formControlName="notas" maxlength="1000"></textarea>
      </label>

      @if (error()) {
        <p class="text-sm text-rojo" role="alert">{{ error() }}</p>
      }

      <div class="flex flex-wrap justify-end gap-3">
        <button class="boton-secundario" type="button" (click)="guardar(false)" [disabled]="enviando()">{{ 'nueva.borrador' | transloco }}</button>
        <button class="boton-primario" type="submit" [disabled]="enviando()">{{ 'nueva.enviar' | transloco }}</button>
      </div>
    </form>
  `,
})
export class Nueva {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly catalogo = inject(CatalogoRepositorio);
  private readonly repo = inject(RequisicionesRepositorio);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);

  protected readonly limite = LIMITE_UNA_FIRMA;
  protected readonly proveedores = signal<Proveedor[]>([]);
  protected readonly productos = signal<Producto[]>([]);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    proveedorId: this.fb.control('', Validators.required),
    notas: this.fb.control(''),
    renglones: this.fb.array<FormRenglon>([this.nuevoRenglon()], [Validators.minLength(1), sinProductosRepetidos]),
  });

  get renglones(): FormArray<FormRenglon> {
    return this.form.controls.renglones;
  }

  private readonly valor = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** Estimado con los precios del catálogo; el servidor recalcula al guardar. */
  protected readonly total = computed(() => {
    const precios = new Map(this.productos().map((p) => [p.id, p.precioUnitario]));
    return (this.valor().renglones ?? []).reduce((s, r) => s + (precios.get(r.productoId ?? '') ?? 0) * (Number(r.cantidad) || 0), 0);
  });

  constructor() {
    void this.catalogo.proveedoresActivos().then((p) => this.proveedores.set(p));
  }

  nuevoRenglon(): FormRenglon {
    return this.fb.group({
      productoId: this.fb.control('', Validators.required),
      cantidad: this.fb.control(1, [Validators.required, Validators.min(1), Validators.max(100_000), Validators.pattern(/^\d+$/)]),
    });
  }

  agregar(): void {
    this.renglones.push(this.nuevoRenglon());
  }

  quitar(i: number): void {
    if (this.renglones.length > 1) this.renglones.removeAt(i);
  }

  async cambiarProveedor(): Promise<void> {
    // Al cambiar de proveedor, los productos elegidos ya no aplican.
    this.renglones.controls.forEach((r) => r.controls.productoId.setValue(''));
    this.productos.set(await this.catalogo.productosActivos(this.form.controls.proveedorId.value));
  }

  protected invalido(r: FormRenglon, campo: 'cantidad' | 'productoId'): boolean {
    const c = r.controls[campo];
    return c.invalid && (c.touched || c.dirty);
  }

  protected precio(r: FormRenglon): number | null {
    return this.productos().find((p) => p.id === r.controls.productoId.value)?.precioUnitario ?? null;
  }

  protected importe(r: FormRenglon): number | null {
    const p = this.precio(r);
    return p === null ? null : p * (Number(r.controls.cantidad.value) || 0);
  }

  async guardar(enviar: boolean): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.enviando.set(true);
    this.error.set(null);
    const v = this.form.getRawValue();
    try {
      const r = await this.repo.crear({
        proveedorId: v.proveedorId,
        notas: v.notas.trim() || null,
        renglones: v.renglones.map((x) => ({ productoId: x.productoId, cantidad: Number(x.cantidad) })),
        enviar,
      });
      await this.router.navigate(['/'], enviar ? { queryParams: { enviada: `REQ-${String(r.folio).padStart(6, '0')}`, firmas: r.firmasNecesarias } } : {});
    } catch (e) {
      const clave = `errores.${e instanceof ErrorApi ? e.codigo : 'otro'}`;
      const texto = this.i18n.translate(clave);
      this.error.set(texto === clave ? this.i18n.translate('errores.otro') : texto);
    } finally {
      this.enviando.set(false);
    }
  }
}
