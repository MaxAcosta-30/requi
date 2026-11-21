import { Component, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Etiqueta visible + control proyectado + error junto al campo (al salir del campo o al enviar).
 * El control proyectado debe usar el mismo `id` y `aria-describedby="{id}-error"`.
 */
@Component({
  selector: 'app-campo',
  host: { class: 'block' },
  imports: [TranslocoPipe],
  template: `
    <div class="space-y-1">
      <label class="block text-sm font-semibold" [for]="idCampo()">{{ etiqueta() | transloco }}</label>
      <ng-content />
      @if (mensaje(); as m) {
        <p class="text-xs text-rojo" [id]="idCampo() + '-error'">{{ m | transloco }}</p>
      }
    </div>
  `,
})
export class Campo {
  readonly idCampo = input.required<string>();
  readonly etiqueta = input.required<string>();
  readonly control = input.required<AbstractControl>();
  /** Clave de traducción por tipo de error de validación. */
  readonly errores = input<Record<string, string>>({});

  protected mensaje(): string | null {
    const c = this.control();
    if (!c.invalid || !(c.touched || c.dirty)) return null;
    const tipo = Object.keys(c.errors ?? {}).find((k) => this.errores()[k]);
    return tipo ? this.errores()[tipo] : null;
  }
}
