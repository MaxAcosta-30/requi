import { Injectable, inject } from '@angular/core';
import { Api } from '../api/api';
import { listarProductos } from '../api/fn/productos/listar-productos';
import { listarProveedores } from '../api/fn/proveedores/listar-proveedores';
import { Producto, Proveedor } from './modelos';
import { aErrorApi } from './requisiciones';

@Injectable({ providedIn: 'root' })
export class CatalogoRepositorio {
  private readonly api = inject(Api);

  async proveedoresActivos(): Promise<Proveedor[]> {
    try {
      return (await this.api.invoke(listarProveedores, { activos: true })).map((p) => ({ id: p.id, nombre: p.nombre, activo: p.activo }));
    } catch (e) {
      throw aErrorApi(e);
    }
  }

  async productosActivos(proveedorId: string): Promise<Producto[]> {
    try {
      return (await this.api.invoke(listarProductos, { activos: true, proveedorId })).map((p) => ({
        id: p.id,
        proveedorId: p.proveedorId,
        sku: p.sku,
        nombre: p.nombre,
        unidad: p.unidad,
        precioUnitario: Number(p.precioUnitario),
      }));
    } catch (e) {
      throw aErrorApi(e);
    }
  }
}
