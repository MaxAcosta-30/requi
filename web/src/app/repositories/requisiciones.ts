import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Api } from '../api/api';
import { aprobarRequisicion } from '../api/fn/requisiciones/aprobar-requisicion';
import { cancelarRequisicion } from '../api/fn/requisiciones/cancelar-requisicion';
import { crearRequisicion } from '../api/fn/requisiciones/crear-requisicion';
import { listarRequisiciones } from '../api/fn/requisiciones/listar-requisiciones';
import { obtenerRequisicion } from '../api/fn/requisiciones/obtener-requisicion';
import { rechazarRequisicion } from '../api/fn/requisiciones/rechazar-requisicion';
import { RequisicionDto } from '../api/models/requisicion-dto';
import { ErrorApi, EstadoRequisicion, NuevaRequisicion, Requisicion } from './modelos';

export type Vista = 'mias' | 'por-aprobar' | 'todas';

export function aRequisicion(d: RequisicionDto): Requisicion {
  return {
    id: d.id,
    folio: Number(d.folio),
    estado: d.estado as EstadoRequisicion,
    proveedorId: d.proveedorId,
    proveedor: d.proveedor,
    solicitanteId: d.solicitanteId,
    total: Number(d.total),
    notas: d.notas,
    motivoRechazo: d.motivoRechazo,
    creadaEn: new Date(d.creadaEn),
    enviadaEn: d.enviadaEn ? new Date(d.enviadaEn) : null,
    firmasNecesarias: Number(d.firmasNecesarias),
    firmas: d.firmas.map((f) => ({
      aprobadorId: f.aprobadorId,
      decision: f.decision as 'Approved' | 'Rejected',
      motivo: f.motivo,
      fecha: new Date(f.fecha),
    })),
    renglones: d.renglones.map((r) => ({
      productoId: r.productoId,
      producto: r.producto,
      cantidad: Number(r.cantidad),
      precioUnitario: Number(r.precioUnitario),
      importe: Number(r.importe),
    })),
  };
}

/** Convierte un ProblemDetails de la API en ErrorApi con su código. */
export function aErrorApi(e: unknown): ErrorApi {
  if (e instanceof HttpErrorResponse) {
    const cuerpo = (e.error ?? {}) as { detail?: string; title?: string; codigo?: string; errors?: Record<string, string[]> };
    return new ErrorApi(e.status, cuerpo.codigo ?? (e.status === 400 ? 'validacion' : 'red'), cuerpo.detail ?? cuerpo.title ?? e.message, cuerpo.errors ?? {});
  }
  return new ErrorApi(0, 'desconocido', String(e));
}

@Injectable({ providedIn: 'root' })
export class RequisicionesRepositorio {
  private readonly api = inject(Api);

  async listar(vista: Vista): Promise<Requisicion[]> {
    return this.llamar(async () => (await this.api.invoke(listarRequisiciones, { vista })).map(aRequisicion));
  }

  async obtener(id: string): Promise<Requisicion> {
    return this.llamar(async () => aRequisicion(await this.api.invoke(obtenerRequisicion, { id })));
  }

  async crear(nueva: NuevaRequisicion): Promise<Requisicion> {
    return this.llamar(async () => aRequisicion(await this.api.invoke(crearRequisicion, { body: nueva })));
  }

  async aprobar(id: string): Promise<Requisicion> {
    return this.llamar(async () => aRequisicion(await this.api.invoke(aprobarRequisicion, { id })));
  }

  async rechazar(id: string, motivo: string): Promise<Requisicion> {
    return this.llamar(async () => aRequisicion(await this.api.invoke(rechazarRequisicion, { id, body: { motivo } })));
  }

  async cancelar(id: string): Promise<Requisicion> {
    return this.llamar(async () => aRequisicion(await this.api.invoke(cancelarRequisicion, { id })));
  }

  private async llamar<T>(fn: () => Promise<T>): Promise<T> {
    try {
      return await fn();
    } catch (e) {
      throw aErrorApi(e);
    }
  }
}
