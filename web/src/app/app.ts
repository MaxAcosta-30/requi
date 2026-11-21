import { Component, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiStatusService } from './core/api-status';
import { SesionService } from './core/auth/sesion';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {
  protected readonly api = inject(ApiStatusService);
  protected readonly sesion = inject(SesionService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);
  protected readonly idioma = toSignal(this.i18n.langChanges$, { initialValue: this.i18n.getActiveLang() });

  protected readonly enlacesCompras = [
    { ruta: '/compras/catalogo', clave: 'app.nav.catalogo' },
    { ruta: '/compras/ordenes', clave: 'app.nav.ordenes' },
    { ruta: '/compras/avisos', clave: 'app.nav.avisos' },
    { ruta: '/compras/mensajes', clave: 'app.nav.mensajes' },
  ];

  constructor() {
    this.api.check();

    // Arranque en frío: nada de OIDC (ni el documento de descubrimiento) hasta que la API responda.
    effect(() => {
      if (this.api.status() === 'ready' && this.sesion.estado() === 'sin-revisar') {
        this.sesion.revisar();
      }
    });

    // Tras canjear el código, se quita ?code=…&state=… de la barra de direcciones.
    effect(() => {
      if (this.sesion.estado() === 'lista' && location.pathname === '/callback') {
        void this.router.navigateByUrl('/', { replaceUrl: true });
      }
    });

    // Caso 8: el idioma cambia sin recargar; el atributo lang sigue al idioma activo.
    effect(() => {
      document.documentElement.lang = this.idioma();
    });
  }

  cambiarIdioma(idioma: string): void {
    this.i18n.setActiveLang(idioma);
  }
}
