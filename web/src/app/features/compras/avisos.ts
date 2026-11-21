import { Component, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';
import { FechaPipe } from '../../core/i18n/i18n';
import { ComprasRepositorio } from '../../repositories/compras';
import { Aviso, ProveedorAdmin } from '../../repositories/modelos';
import { Estado, EstadoCarga } from './estado';

/** Caso 6: bandeja del proveedor. No tiene cuenta; la lee Compras. */
@Component({
  selector: 'app-avisos',
  imports: [TranslocoPipe, FechaPipe, Estado],
  template: `
    <h1 class="text-3xl sm:text-4xl font-extrabold tracking-tight">{{ 'avisos.titulo' | transloco }}</h1>
    <p class="text-tinta-2 mt-1 mb-6">{{ 'avisos.subtitulo' | transloco }}</p>

    @if (sesion.tieneRol('Compras')) {
      <div class="mb-4 max-w-sm">
        <label class="block text-sm font-semibold mb-1" for="filtro-proveedor">{{ 'avisos.proveedor' | transloco }}</label>
        <select id="filtro-proveedor" class="campo" [value]="proveedorId()" (change)="elegir($any($event.target).value)">
          <option value="">{{ 'avisos.todos' | transloco }}</option>
          @for (p of proveedores(); track p.id) {
            <option [value]="p.id">{{ p.nombre }}</option>
          }
        </select>
      </div>
    }

    <app-estado [estado]="estado()" [vacio]="avisos().length === 0" vacioTitulo="avisos.vacio" vacioTexto="avisos.vacioTexto" (reintentar)="cargar()">
      <ul class="space-y-3">
        @for (a of avisos(); track a.id) {
          <li class="hoja p-4">
            <details>
              <summary class="cursor-pointer min-h-11 flex flex-wrap items-center gap-x-4 gap-y-1">
                <span class="font-semibold">{{ a.asunto }}</span>
                <span class="text-sm text-tinta-2">{{ a.proveedor }} · {{ a.creadoEn | fecha }}</span>
                <span class="cifra text-xs text-tinta-3">{{ 'avisos.orden' | transloco }} {{ a.ordenId.slice(0, 8) }}</span>
              </summary>
              <p class="mt-3 whitespace-pre-line text-sm text-tinta-2 border-t border-renglon pt-3">{{ a.cuerpo }}</p>
            </details>
          </li>
        }
      </ul>
    </app-estado>
  `,
})
export class Avisos {
  protected readonly sesion = inject(SesionService);
  private readonly repo = inject(ComprasRepositorio);
  protected readonly estado = signal<EstadoCarga>('cargando');
  protected readonly avisos = signal<Aviso[]>([]);
  protected readonly proveedores = signal<ProveedorAdmin[]>([]);
  protected readonly proveedorId = signal('');

  constructor() {
    if (this.sesion.tieneRol('Compras')) {
      void this.repo.proveedores().then((p) => this.proveedores.set(p)).catch(() => undefined);
    }
    void this.cargar();
  }

  elegir(id: string): void {
    this.proveedorId.set(id);
    void this.cargar();
  }

  async cargar(): Promise<void> {
    if (!this.sesion.tieneRol('Compras')) return;
    this.estado.set('cargando');
    try {
      this.avisos.set(await this.repo.avisos(this.proveedorId() || undefined));
      this.estado.set('lista');
    } catch {
      this.estado.set('error');
    }
  }
}
