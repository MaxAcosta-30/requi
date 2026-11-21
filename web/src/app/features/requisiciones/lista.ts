import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';
import { DineroPipe } from '../../core/i18n/i18n';
import { Requisicion } from '../../repositories/modelos';
import { RequisicionesRepositorio, Vista } from '../../repositories/requisiciones';
import { Casillas } from './casillas';

export const FRANJA: Record<string, string> = {
  Draft: 'bg-tinta-3/30',
  Submitted: 'bg-canario',
  Approved: 'bg-menta',
  OrderGenerated: 'bg-cielo',
  Rejected: 'bg-rosa',
  Cancelled: 'bg-gris',
};

/** Casos 3 y 4: lo que pidió el solicitante, lo que espera firma y todo (Compras). */
@Component({
  selector: 'app-lista',
  imports: [RouterLink, TranslocoPipe, DineroPipe, Casillas],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4 mb-6">
      <div>
        <h1 class="text-3xl sm:text-4xl font-extrabold tracking-tight">{{ titulo() | transloco }}</h1>
        <p class="text-tinta-2 mt-1">{{ 'lista.subtitulo' | transloco }}</p>
      </div>
      @if (sesion.tieneRol('Solicitante')) {
        <a routerLink="/nueva" class="boton-primario">{{ 'lista.nueva' | transloco }}</a>
      }
    </div>

    @if (enviada()) {
      <p role="status" class="mb-4 rounded bg-menta px-4 py-3 text-sm font-medium">
        {{ 'lista.enviada' | transloco: { folio: enviada(), firmas: firmas() } }}
      </p>
    }

    @switch (estado()) {
      @case ('cargando') {
        <div class="hoja p-5 space-y-3" aria-busy="true">
          <div class="h-3 w-1/3 rounded bg-renglon animate-pulse motion-reduce:animate-none"></div>
          <div class="h-3 rounded bg-renglon animate-pulse motion-reduce:animate-none"></div>
        </div>
      }
      @case ('error') {
        <div class="hoja p-5 space-y-3 ring-1 ring-rojo/40" role="alert">
          <p class="font-semibold text-rojo">{{ 'lista.error' | transloco }}</p>
          <p class="text-sm text-tinta-2">{{ 'lista.errorTexto' | transloco }}</p>
          <button type="button" class="boton-secundario" (click)="cargar()">{{ 'arranque.reintentar' | transloco }}</button>
        </div>
      }
      @default {
        @if (requisiciones().length === 0) {
          <div class="hoja p-5 space-y-3">
            <p class="font-semibold">{{ (vista() === 'mias' ? 'lista.vacio' : 'lista.vacioPorAprobar') | transloco }}</p>
            @if (vista() === 'mias') {
              <p class="text-sm text-tinta-2">{{ 'lista.vacioTexto' | transloco }}</p>
            }
          </div>
        } @else {
          <ul class="space-y-3">
            @for (r of requisiciones(); track r.id) {
              <li class="hoja relative grid gap-3 p-4 sm:grid-cols-[8.5rem_1fr_auto_auto] sm:items-center sm:gap-6">
                <span aria-hidden="true" class="absolute left-0 top-3 bottom-3 w-1.5 rounded-r" [class]="franja(r)"></span>
                <p class="cifra text-sm text-tinta-3">{{ r.estado === 'Draft' ? ('lista.sinFolio' | transloco) : folio(r) }}</p>
                <div>
                  <a [routerLink]="['/requisiciones', r.id]" class="font-semibold text-tinta no-underline hover:underline">{{ r.proveedor }}</a>
                  <p class="text-sm text-tinta-2">
                    {{ r.renglones.length === 1 ? ('lista.renglon' | transloco) : ('lista.renglones' | transloco: { n: r.renglones.length }) }}
                  </p>
                  @if (r.motivoRechazo) {
                    <p class="text-sm text-rojo mt-1">{{ r.motivoRechazo }}</p>
                  }
                </div>
                <div class="flex items-center gap-3">
                  <span class="rounded px-2.5 py-1 text-xs font-semibold" [class]="franja(r)">{{ 'estado.' + r.estado | transloco }}</span>
                  <app-casillas [r]="r" [etiqueta]="r.estado === 'Rejected' ? ('lista.rechazada' | transloco) : ('lista.firmas' | transloco: { hechas: firmadas(r), total: r.firmasNecesarias })" />
                </div>
                <p class="cifra text-lg font-semibold text-carbon sm:text-right">{{ r.total | dinero }}</p>
              </li>
            }
          </ul>
        }
      }
    }
  `,
})
export class Lista {
  protected readonly sesion = inject(SesionService);
  private readonly repo = inject(RequisicionesRepositorio);

  /** Viene de la ruta (data.vista) con withComponentInputBinding. */
  readonly vista = input<Vista>('mias');
  /** Aviso tras enviar (query params ?enviada=REQ-000143&firmas=2). */
  readonly enviada = input<string | undefined>(undefined);
  readonly firmas = input<string | undefined>(undefined);

  protected readonly estado = signal<'cargando' | 'error' | 'lista'>('cargando');
  protected readonly requisiciones = signal<Requisicion[]>([]);
  protected readonly titulo = computed(() =>
    this.vista() === 'mias' ? 'lista.titulo' : this.vista() === 'por-aprobar' ? 'lista.tituloPorAprobar' : 'lista.tituloTodas',
  );

  constructor() {
    effect(() => {
      this.vista();
      void this.cargar();
    });
  }

  async cargar(): Promise<void> {
    this.estado.set('cargando');
    try {
      this.requisiciones.set(await this.repo.listar(this.vista()));
      this.estado.set('lista');
    } catch {
      this.estado.set('error');
    }
  }

  protected franja(r: Requisicion): string {
    return FRANJA[r.estado] ?? '';
  }

  protected folio(r: Requisicion): string {
    return `REQ-${String(r.folio).padStart(6, '0')}`;
  }

  protected firmadas(r: Requisicion): number {
    return r.firmas.filter((f) => f.decision === 'Approved').length;
  }
}
