import { Component, inject, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';

export type EstadoCarga = 'cargando' | 'error' | 'lista';

/** Estados comunes de las pantallas de Compras: sin permisos, carga, error, vacío y contenido. */
@Component({
  selector: 'app-estado',
  host: { class: 'block' },
  imports: [TranslocoPipe],
  template: `
    @if (!sesion.tieneRol('Compras')) {
      <div class="hoja p-5 space-y-2" role="alert">
        <p class="font-semibold">{{ 'compras.sinPermisos' | transloco }}</p>
        <p class="text-sm text-tinta-2">{{ 'compras.sinPermisosTexto' | transloco }}</p>
      </div>
    } @else if (estado() === 'cargando') {
      <div class="hoja p-5 space-y-3" aria-busy="true" role="status">
        <span class="sr-only">{{ 'compras.cargando' | transloco }}</span>
        <div class="h-3 w-1/3 rounded bg-renglon animate-pulse motion-reduce:animate-none"></div>
        <div class="h-3 rounded bg-renglon animate-pulse motion-reduce:animate-none"></div>
        <div class="h-3 w-2/3 rounded bg-renglon animate-pulse motion-reduce:animate-none"></div>
      </div>
    } @else if (estado() === 'error') {
      <div class="hoja p-5 space-y-3 ring-1 ring-rojo/40" role="alert">
        <p class="font-semibold text-rojo">{{ 'compras.error' | transloco }}</p>
        <p class="text-sm text-tinta-2">{{ 'compras.errorTexto' | transloco }}</p>
        <button type="button" class="boton-secundario" (click)="reintentar.emit()">{{ 'arranque.reintentar' | transloco }}</button>
      </div>
    } @else if (vacio()) {
      <div class="hoja p-5 space-y-1">
        <p class="font-semibold">{{ vacioTitulo() | transloco }}</p>
        <p class="text-sm text-tinta-2">{{ vacioTexto() | transloco }}</p>
      </div>
    } @else {
      <ng-content />
    }
  `,
})
export class Estado {
  protected readonly sesion = inject(SesionService);
  readonly estado = input.required<EstadoCarga>();
  readonly vacio = input(false);
  readonly vacioTitulo = input('compras.vacio');
  readonly vacioTexto = input('compras.vacioTexto');
  readonly reintentar = output<void>();
}
