import { Component, effect, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { ApiStatusService } from './core/api-status';
import { SesionService } from './core/auth/sesion';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {
  protected readonly api = inject(ApiStatusService);
  protected readonly sesion = inject(SesionService);
  private readonly router = inject(Router);

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
  }
}
