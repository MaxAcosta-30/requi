// Tipos propios de la interfaz: los componentes no dependen del cliente generado.

export type EstadoRequisicion = 'Draft' | 'Submitted' | 'Approved' | 'OrderGenerated' | 'Rejected' | 'Cancelled';

export interface Firma {
  aprobadorId: string;
  decision: 'Approved' | 'Rejected';
  motivo: string | null;
  fecha: Date;
}

export interface Renglon {
  productoId: string;
  producto: string;
  cantidad: number;
  precioUnitario: number;
  importe: number;
}

export interface Requisicion {
  id: string;
  folio: number;
  estado: EstadoRequisicion;
  proveedorId: string;
  proveedor: string;
  solicitanteId: string;
  total: number;
  notas: string | null;
  motivoRechazo: string | null;
  creadaEn: Date;
  enviadaEn: Date | null;
  firmasNecesarias: number;
  firmas: Firma[];
  renglones: Renglon[];
}

export interface Proveedor {
  id: string;
  nombre: string;
  activo: boolean;
}

export interface Producto {
  id: string;
  proveedorId: string;
  sku: string;
  nombre: string;
  unidad: string;
  precioUnitario: number;
}

export interface NuevaRequisicion {
  proveedorId: string;
  notas: string | null;
  renglones: { productoId: string; cantidad: number }[];
  enviar: boolean;
}

/** Error de la API con el código de la regla de negocio (ProblemDetails.codigo). */
export class ErrorApi extends Error {
  constructor(
    readonly estado: number,
    readonly codigo: string,
    mensaje: string,
    readonly campos: Record<string, string[]> = {},
  ) {
    super(mensaje);
  }
}

/** Arriba de este monto se necesitan 2 firmas (la API es la que decide; esto solo anticipa). */
export const LIMITE_UNA_FIRMA = 20_000;
