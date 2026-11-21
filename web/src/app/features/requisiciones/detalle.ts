import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';
import { DineroPipe, FechaPipe } from '../../core/i18n/i18n';
import { ErrorApi, LIMITE_UNA_FIRMA, Requisicion } from '../../repositories/modelos';
import { RequisicionesRepositorio } from '../../repositories/requisiciones';
import { Casillas } from './casillas';
import { FRANJA } from './lista';

/** Caso 4: el aprobador ve el documento con sus casillas de firma y decide. */
@Component({
  selector: 'app-detalle',
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe, DineroPipe, FechaPipe, Casillas],
  template: `
    <a routerLink="/" class="text-sm text-carbon underline underline-offset-2">{{ 'detalle.volver' | transloco }}</a>
    @if (r(); as r) {
      <div class="mt-4 grid gap-6 lg:grid-cols-[1fr_20rem] items-start">
        <article class="hoja p-6 sm:p-8 space-y-6" [attr.aria-labelledby]="'t-' + r.id">
          <header class="flex flex-wrap items-start justify-between gap-3 border-b border-renglon pb-4">
            <div>
              <p class="cifra text-sm text-tinta-3">{{ folio(r) }} · {{ r.creadaEn | fecha }}</p>
              <h1 [id]="'t-' + r.id" class="text-2xl font-extrabold tracking-tight">{{ r.proveedor }}</h1>
              @if (r.notas) {
                <p class="text-sm text-tinta-2 mt-1">{{ r.notas }}</p>
              }
            </div>
            <span class="rounded px-2.5 py-1 text-sm font-semibold" [class]="franja[r.estado]">{{ 'estado.' + r.estado | transloco }}</span>
          </header>
          <table class="w-full text-sm">
            <thead class="text-xs text-tinta-3 text-left">
              <tr><th class="font-semibold pb-2">{{ 'nueva.producto' | transloco }}</th><th class="font-semibold pb-2 text-right">{{ 'nueva.cantidad' | transloco }}</th><th class="font-semibold pb-2 text-right">{{ 'nueva.importe' | transloco }}</th></tr>
            </thead>
            <tbody class="divide-y divide-renglon">
              @for (l of r.renglones; track l.productoId) {
                <tr><td class="py-2">{{ l.producto }}</td><td class="cifra text-right">{{ l.cantidad }}</td><td class="cifra text-right">{{ l.importe | dinero }}</td></tr>
              }
            </tbody>
            <tfoot><tr><td colspan="2" class="pt-4 font-semibold">{{ 'detalle.total' | transloco }}</td><td class="cifra pt-4 text-right text-2xl font-semibold text-carbon">{{ r.total | dinero }}</td></tr></tfoot>
          </table>
          <div>
            <p class="text-sm font-semibold mb-2">
              {{ 'detalle.firmas' | transloco }}
              <span class="font-normal text-tinta-3">· {{ (r.total > limite ? 'detalle.firmasPorque2' : 'detalle.firmasPorque1') | transloco }}</span>
            </p>
            <app-casillas [r]="r" [etiqueta]="'lista.firmas' | transloco: { hechas: firmadas(r), total: r.firmasNecesarias }" />
            @if (r.motivoRechazo) {
              <p class="mt-3 text-sm text-rojo">{{ r.motivoRechazo }}</p>
            }
          </div>
        </article>

        <aside class="hoja p-5 space-y-4 lg:sticky lg:top-6" aria-label="Decisión">
          @if (puedeDecidir()) {
            <button class="boton-primario w-full" type="button" (click)="aprobar()" [disabled]="ocupado()">{{ 'detalle.aprobar' | transloco }}</button>
            <form class="space-y-2" (ngSubmit)="rechazar()">
              <label class="block space-y-1">
                <span class="text-sm font-semibold">{{ 'detalle.motivo' | transloco }}</span>
                <textarea class="campo py-2 min-h-24" [formControl]="motivo" maxlength="500"
                  [attr.aria-invalid]="motivoInvalido()" [attr.aria-describedby]="motivoInvalido() ? 'error-motivo' : null"
                  [class.ring-2]="motivoInvalido()" [class.ring-rojo]="motivoInvalido()"></textarea>
              </label>
              @if (motivoInvalido()) {
                <p id="error-motivo" class="text-xs text-rojo">{{ 'detalle.motivoError' | transloco }}</p>
              }
              <button class="boton-secundario w-full text-rojo ring-rojo/50" type="submit" [disabled]="ocupado()">{{ 'detalle.rechazar' | transloco }}</button>
            </form>
          } @else if (esPropia() && sesion.tieneRol('Aprobador')) {
            <p class="text-sm text-tinta-2">{{ 'detalle.propia' | transloco }}</p>
          } @else if (yaDecidi()) {
            <p class="text-sm text-tinta-2">{{ 'detalle.yaDecidiste' | transloco }}</p>
          }
          @if (puedeCancelar()) {
            <button class="boton-secundario w-full" type="button" (click)="cancelar()" [disabled]="ocupado()">{{ 'detalle.cancelar' | transloco }}</button>
          }
          @if (error()) {
            <p class="text-sm text-rojo" role="alert">{{ error() }}</p>
          }
        </aside>
      </div>
    } @else if (error()) {
      <p class="mt-4 text-rojo" role="alert">{{ error() }}</p>
    } @else {
      <div class="hoja mt-4 h-48 animate-pulse motion-reduce:animate-none" aria-busy="true"></div>
    }
  `,
})
export class Detalle {
  protected readonly sesion = inject(SesionService);
  private readonly repo = inject(RequisicionesRepositorio);
  private readonly i18n = inject(TranslocoService);

  readonly id = input.required<string>();
  protected readonly limite = LIMITE_UNA_FIRMA;
  protected readonly franja = FRANJA;
  protected readonly r = signal<Requisicion | null>(null);
  protected readonly ocupado = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly motivo = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/\S/)] });
  private readonly intentoRechazo = signal(false);

  private readonly yo = computed(() => this.sesion.usuario()?.id ?? '');
  protected readonly esPropia = computed(() => this.r()?.solicitanteId === this.yo());
  protected readonly yaDecidi = computed(() => this.r()?.firmas.some((f) => f.aprobadorId === this.yo()) ?? false);
  protected readonly puedeDecidir = computed(
    () => this.sesion.tieneRol('Aprobador') && this.r()?.estado === 'Submitted' && !this.esPropia() && !this.yaDecidi(),
  );
  protected readonly puedeCancelar = computed(() => this.esPropia() && ['Draft', 'Submitted'].includes(this.r()?.estado ?? ''));
  protected readonly motivoInvalido = computed(() => this.intentoRechazo() && this.motivo.invalid);

  constructor() {
    effect(() => void this.cargar(this.id()));
  }

  private async cargar(id: string): Promise<void> {
    try {
      this.r.set(await this.repo.obtener(id));
    } catch (e) {
      this.mostrar(e);
    }
  }

  async aprobar(): Promise<void> {
    await this.accion(() => this.repo.aprobar(this.id()));
  }

  async rechazar(): Promise<void> {
    this.intentoRechazo.set(true);
    this.motivo.markAsTouched();
    if (this.motivo.invalid) return;
    await this.accion(() => this.repo.rechazar(this.id(), this.motivo.value.trim()));
  }

  async cancelar(): Promise<void> {
    await this.accion(() => this.repo.cancelar(this.id()));
  }

  private async accion(fn: () => Promise<Requisicion>): Promise<void> {
    this.ocupado.set(true);
    this.error.set(null);
    try {
      this.r.set(await fn());
    } catch (e) {
      this.mostrar(e);
    } finally {
      this.ocupado.set(false);
    }
  }

  /** Los 409 traen un código de regla: se muestra su texto traducido, no el de la API. */
  private mostrar(e: unknown): void {
    const codigo = e instanceof ErrorApi ? e.codigo : 'otro';
    const clave = `errores.${codigo}`;
    const texto = this.i18n.translate(clave);
    this.error.set(texto === clave ? this.i18n.translate('errores.otro') : texto);
  }

  protected folio(r: Requisicion): string {
    return r.estado === 'Draft' ? '' : `REQ-${String(r.folio).padStart(6, '0')}`;
  }

  protected firmadas(r: Requisicion): number {
    return r.firmas.filter((f) => f.decision === 'Approved').length;
  }
}
