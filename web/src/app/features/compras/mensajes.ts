import { Component, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { SesionService } from '../../core/auth/sesion';
import { FechaHoraPipe } from '../../core/i18n/i18n';
import { ComprasRepositorio } from '../../repositories/compras';
import { EstadoMensaje, Mensaje } from '../../repositories/modelos';
import { Estado, EstadoCarga } from './estado';

export const ESTADOS_MENSAJE: EstadoMensaje[] = ['Failed', 'Pending', 'Processed'];

/** Caso 6: mensajes del Outbox. Los fallidos (5 intentos) quedan aquí; no hay reintento manual. */
@Component({
  selector: 'app-mensajes',
  imports: [TranslocoPipe, FechaHoraPipe, Estado],
  template: `
    <h1 class="text-3xl sm:text-4xl font-extrabold tracking-tight">{{ 'mensajes.titulo' | transloco }}</h1>
    <p class="text-tinta-2 mt-1 mb-6 max-w-2xl">{{ 'mensajes.ayuda' | transloco }}</p>

    @if (sesion.tieneRol('Compras')) {
      <div class="mb-4 max-w-sm">
        <label class="block text-sm font-semibold mb-1" for="filtro-estado">{{ 'mensajes.estado' | transloco }}</label>
        <select id="filtro-estado" class="campo" [value]="filtro()" (change)="elegir($any($event.target).value)">
          @for (e of estados; track e) {
            <option [value]="e">{{ 'mensajes.estados.' + e | transloco }}</option>
          }
          <option value="">{{ 'mensajes.todos' | transloco }}</option>
        </select>
      </div>
    }

    <app-estado [estado]="estado()" [vacio]="mensajes().length === 0" vacioTitulo="mensajes.vacio" vacioTexto="mensajes.vacioTexto" (reintentar)="cargar()">
      <ul class="space-y-3">
        @for (m of mensajes(); track m.id) {
          <li class="hoja p-4 space-y-2">
            <div class="flex flex-wrap items-center gap-x-4 gap-y-1">
              <p class="font-semibold">{{ m.tipo }}</p>
              <span class="rounded px-2.5 py-1 text-xs font-semibold" [class]="franja(m.estado)">{{ 'mensajes.estados.' + m.estado | transloco }}</span>
              <p class="cifra text-sm text-tinta-2">{{ 'mensajes.intentos' | transloco: { n: m.intentos } }}</p>
            </div>
            @if (m.ultimoError) {
              <p class="text-sm text-rojo break-words"><span class="font-semibold">{{ 'mensajes.ultimoError' | transloco }}:</span> {{ m.ultimoError }}</p>
            }
            <p class="cifra text-xs text-tinta-3">
              {{ 'mensajes.ocurrio' | transloco }} {{ m.ocurrioEn | fechaHora }}
              @if (m.procesadoEn) { · {{ 'mensajes.procesado' | transloco }} {{ m.procesadoEn | fechaHora }} }
            </p>
          </li>
        }
      </ul>
    </app-estado>
  `,
})
export class Mensajes {
  protected readonly sesion = inject(SesionService);
  private readonly repo = inject(ComprasRepositorio);
  protected readonly estados = ESTADOS_MENSAJE;
  protected readonly estado = signal<EstadoCarga>('cargando');
  protected readonly mensajes = signal<Mensaje[]>([]);
  protected readonly filtro = signal<string>('Failed');

  constructor() {
    void this.cargar();
  }

  elegir(valor: string): void {
    this.filtro.set(valor);
    void this.cargar();
  }

  async cargar(): Promise<void> {
    if (!this.sesion.tieneRol('Compras')) return;
    this.estado.set('cargando');
    try {
      this.mensajes.set(await this.repo.mensajes(this.filtro() || undefined));
      this.estado.set('lista');
    } catch {
      this.estado.set('error');
    }
  }

  protected franja(estado: string): string {
    return estado === 'Failed' ? 'bg-rosa' : estado === 'Processed' ? 'bg-menta' : 'bg-canario';
  }
}
