import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';
import { DineroPipe, FechaPipe } from '../../core/i18n/i18n';
import { ComprasRepositorio } from '../../repositories/compras';
import { Orden } from '../../repositories/modelos';
import { Estado, EstadoCarga } from './estado';

/** Caso 5: órdenes de compra generadas. */
@Component({
  selector: 'app-ordenes',
  imports: [RouterLink, TranslocoPipe, DineroPipe, FechaPipe, Estado],
  template: `
    <h1 class="text-3xl sm:text-4xl font-extrabold tracking-tight">{{ 'ordenes.titulo' | transloco }}</h1>
    <p class="text-tinta-2 mt-1 mb-6">{{ 'ordenes.subtitulo' | transloco }}</p>

    <app-estado [estado]="estado()" [vacio]="ordenes().length === 0" vacioTitulo="ordenes.vacio" vacioTexto="ordenes.vacioTexto" (reintentar)="cargar()">
      <ul class="space-y-3">
        @for (o of ordenes(); track o.id) {
          <li class="hoja grid gap-1 p-4 sm:grid-cols-[9rem_1fr_auto_8rem_auto] sm:items-center sm:gap-6">
            <p class="cifra text-sm text-tinta-3">OC-{{ o.folio.toString().padStart(6, '0') }}</p>
            <p class="font-semibold">{{ o.proveedor }}</p>
            <p class="cifra text-lg font-semibold text-carbon sm:text-right">{{ o.total | dinero }}</p>
            <p class="text-sm text-tinta-2">{{ o.creadaEn | fecha }}</p>
            <a [routerLink]="['/requisiciones', o.requisicionId]" class="boton-secundario w-fit">{{ 'ordenes.verRequisicion' | transloco }}</a>
          </li>
        }
      </ul>
    </app-estado>
  `,
})
export class Ordenes {
  private readonly repo = inject(ComprasRepositorio);
  private readonly sesion = inject(SesionService);
  protected readonly estado = signal<EstadoCarga>('cargando');
  protected readonly ordenes = signal<Orden[]>([]);

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    if (!this.sesion.tieneRol('Compras')) return;
    this.estado.set('cargando');
    try {
      this.ordenes.set(await this.repo.ordenes());
      this.estado.set('lista');
    } catch {
      this.estado.set('error');
    }
  }
}
