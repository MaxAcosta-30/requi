import { Injectable, inject } from '@angular/core';
import { Api } from '../api/api';
import { listarAvisosProveedor } from '../api/fn/compras/listar-avisos-proveedor';
import { listarMensajesOutbox } from '../api/fn/compras/listar-mensajes-outbox';
import { listarOrdenes } from '../api/fn/compras/listar-ordenes';
import { crearProducto } from '../api/fn/productos/crear-producto';
import { desactivarProducto } from '../api/fn/productos/desactivar-producto';
import { editarProducto } from '../api/fn/productos/editar-producto';
import { listarProductos } from '../api/fn/productos/listar-productos';
import { crearProveedor } from '../api/fn/proveedores/crear-proveedor';
import { desactivarProveedor } from '../api/fn/proveedores/desactivar-proveedor';
import { editarProveedor } from '../api/fn/proveedores/editar-proveedor';
import { listarProveedores } from '../api/fn/proveedores/listar-proveedores';
import { AvisoDto } from '../api/models/aviso-dto';
import { MensajeDto } from '../api/models/mensaje-dto';
import { OrdenDto } from '../api/models/orden-dto';
import { ProductoDto } from '../api/models/producto-dto';
import { ProveedorDto } from '../api/models/proveedor-dto';
import {
  Aviso,
  ErrorApi,
  Mensaje,
  Orden,
  ProductoAdmin,
  ProductoEntrada,
  ProveedorAdmin,
  ProveedorEntrada,
} from './modelos';
import { aErrorApi } from './requisiciones';

export const aProveedor = (d: ProveedorDto): ProveedorAdmin => ({
  id: d.id,
  nombre: d.nombre,
  correo: d.correo,
  telefono: d.telefono ?? null,
  activo: d.activo,
});

export const aProducto = (d: ProductoDto): ProductoAdmin => ({
  id: d.id,
  proveedorId: d.proveedorId,
  proveedor: d.proveedor,
  sku: d.sku,
  nombre: d.nombre,
  unidad: d.unidad,
  precioUnitario: Number(d.precioUnitario),
  activo: d.activo,
});

export const aOrden = (d: OrdenDto): Orden => ({
  id: d.id,
  folio: Number(d.folio),
  requisicionId: d.requisicionId,
  proveedorId: d.proveedorId,
  proveedor: d.proveedor,
  total: Number(d.total),
  creadaEn: new Date(d.creadaEn),
});

export const aAviso = (d: AvisoDto): Aviso => ({
  id: d.id,
  proveedorId: d.proveedorId,
  proveedor: d.proveedor,
  ordenId: d.ordenId,
  asunto: d.asunto,
  cuerpo: d.cuerpo,
  creadoEn: new Date(d.creadoEn),
});

export const aMensaje = (d: MensajeDto): Mensaje => ({
  id: d.id,
  tipo: d.tipo,
  estado: d.estado,
  intentos: Number(d.intentos),
  ultimoError: d.ultimoError ?? null,
  ocurrioEn: new Date(d.ocurrioEn),
  procesadoEn: d.procesadoEn ? new Date(d.procesadoEn) : null,
});

const CODIGOS_CONOCIDOS = ['duplicado', 'proveedor_invalido', 'referencia'];

/** Clave de traducción (en `errores.*`) para un error de la API; los 409 se distinguen por `codigo`. */
export type ClaveError = `errores.${string}`;

export function claveError(e: unknown): ClaveError {
  if (!(e instanceof ErrorApi)) return 'errores.otro';
  if (e.estado === 403) return 'errores.permisos';
  if (e.estado === 404) return 'errores.no_encontrado';
  if (CODIGOS_CONOCIDOS.includes(e.codigo)) return `errores.${e.codigo}`;
  return e.codigo === 'red' ? 'errores.red' : 'errores.otro';
}

/** Compras: catálogo (con inactivos), órdenes, bandeja del proveedor y mensajes del Outbox. */
@Injectable({ providedIn: 'root' })
export class ComprasRepositorio {
  private readonly api = inject(Api);

  proveedores(activos?: boolean): Promise<ProveedorAdmin[]> {
    return this.llamar(async () => (await this.api.invoke(listarProveedores, { activos })).map(aProveedor));
  }

  crearProveedor(body: ProveedorEntrada): Promise<ProveedorAdmin> {
    return this.llamar(async () => aProveedor(await this.api.invoke(crearProveedor, { body })));
  }

  editarProveedor(id: string, body: ProveedorEntrada): Promise<ProveedorAdmin> {
    return this.llamar(async () => aProveedor(await this.api.invoke(editarProveedor, { id, body })));
  }

  desactivarProveedor(id: string): Promise<void> {
    return this.llamar(async () => void (await this.api.invoke(desactivarProveedor, { id })));
  }

  productos(filtro: { activos?: boolean; proveedorId?: string } = {}): Promise<ProductoAdmin[]> {
    return this.llamar(async () => (await this.api.invoke(listarProductos, filtro)).map(aProducto));
  }

  crearProducto(body: ProductoEntrada): Promise<ProductoAdmin> {
    return this.llamar(async () => aProducto(await this.api.invoke(crearProducto, { body })));
  }

  editarProducto(id: string, body: ProductoEntrada): Promise<ProductoAdmin> {
    return this.llamar(async () => aProducto(await this.api.invoke(editarProducto, { id, body })));
  }

  desactivarProducto(id: string): Promise<void> {
    return this.llamar(async () => void (await this.api.invoke(desactivarProducto, { id })));
  }

  ordenes(): Promise<Orden[]> {
    return this.llamar(async () => (await this.api.invoke(listarOrdenes)).map(aOrden));
  }

  avisos(proveedorId?: string): Promise<Aviso[]> {
    return this.llamar(async () => (await this.api.invoke(listarAvisosProveedor, { proveedorId })).map(aAviso));
  }

  mensajes(estado?: string): Promise<Mensaje[]> {
    return this.llamar(async () => (await this.api.invoke(listarMensajesOutbox, { estado })).map(aMensaje));
  }

  private async llamar<T>(fn: () => Promise<T>): Promise<T> {
    try {
      return await fn();
    } catch (e) {
      throw aErrorApi(e);
    }
  }
}
