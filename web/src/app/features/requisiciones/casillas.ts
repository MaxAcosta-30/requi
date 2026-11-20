import { Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Requisicion } from '../../repositories/modelos';

/** La firma visual del diseño: 1 casilla hasta $20,000, 2 arriba. Firmada, pendiente o rechazada. */
@Component({
  selector: 'app-casillas',
  imports: [TranslocoPipe],
  template: `
    @if (r().estado === 'Draft') {
      <span class="text-xs text-tinta-3">{{ 'lista.sinEnviar' | transloco }}</span>
    } @else {
      <span class="flex gap-1.5" role="img" [attr.aria-label]="etiqueta()">
        @for (c of casillas(); track $index) {
          <span class="grid place-items-center size-9 rounded"
            [class]="c === 'firmada' ? 'border border-renglon text-carbon' : c === 'rechazo' ? 'border border-rojo/50 text-rojo' : 'border-2 border-dashed border-carbon/50'">
            @if (c === 'firmada') {
              <svg class="size-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M3 17c3-1 4-8 6-8s0 7 2 7 3-4 5-4 2 3 5 3"/><path d="M3 21h18"/></svg>
            } @else if (c === 'rechazo') {
              <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>
            }
          </span>
        }
      </span>
    }
  `,
})
export class Casillas {
  readonly r = input.required<Requisicion>();
  readonly etiqueta = input('');

  readonly casillas = computed(() => {
    const r = this.r();
    const aprobadas = r.firmas.filter((f) => f.decision === 'Approved').length;
    if (r.estado === 'Rejected') return ['rechazo'];
    return Array.from({ length: r.firmasNecesarias }, (_, i) => (i < aprobadas ? 'firmada' : 'pendiente'));
  });
}
