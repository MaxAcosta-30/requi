import { Injectable, computed, inject, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

export type Rol = 'Solicitante' | 'Aprobador' | 'Compras';

export interface Usuario {
  id: string;
  nombre: string;
  correo: string;
  roles: Rol[];
}

const ROLES: readonly Rol[] = ['Solicitante', 'Aprobador', 'Compras'];

/** Convierte los claims del id_token en un usuario; "role" puede venir como texto o arreglo. */
export function usuarioDesdeClaims(claims: Record<string, unknown> | null | undefined): Usuario | null {
  if (!claims || typeof claims['sub'] !== 'string') return null;
  const crudos = claims['role'];
  const lista = Array.isArray(crudos) ? crudos : crudos === undefined ? [] : [crudos];
  return {
    id: claims['sub'],
    nombre: typeof claims['name'] === 'string' ? claims['name'] : '',
    correo: typeof claims['email'] === 'string' ? claims['email'] : '',
    roles: lista.filter((r): r is Rol => ROLES.includes(r as Rol)),
  };
}

/** Estado de la sesión con Signals. La API decide los permisos; esto solo adapta la interfaz. */
@Injectable({ providedIn: 'root' })
export class SesionService {
  private readonly oidc = inject(OidcSecurityService);

  readonly estado = signal<'sin-revisar' | 'revisando' | 'lista'>('sin-revisar');
  readonly usuario = signal<Usuario | null>(null);
  readonly autenticado = computed(() => this.usuario() !== null);

  /** Se llama cuando la API ya despertó: canjea el código del callback o recupera la sesión. */
  revisar(): void {
    this.estado.set('revisando');
    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated, userData }) => {
        this.usuario.set(isAuthenticated ? usuarioDesdeClaims(userData) : null);
        this.estado.set('lista');
      },
      error: () => {
        this.usuario.set(null);
        this.estado.set('lista');
      },
    });
  }

  iniciarSesion(): void {
    this.oidc.authorize();
  }

  cerrarSesion(): void {
    this.oidc.logoff().subscribe(() => this.usuario.set(null));
  }

  tieneRol(rol: Rol): boolean {
    return this.usuario()?.roles.includes(rol) ?? false;
  }
}
