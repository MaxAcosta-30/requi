import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ProductosSeccion } from './productos-seccion';
import { ProveedoresSeccion } from './proveedores-seccion';

/** Caso 2: catálogo de Compras (proveedores y productos). */
@Component({
  selector: 'app-catalogo',
  imports: [TranslocoPipe, ProveedoresSeccion, ProductosSeccion],
  template: `
    <h1 class="text-3xl sm:text-4xl font-extrabold tracking-tight">{{ 'catalogo.titulo' | transloco }}</h1>
    <p class="text-tinta-2 mt-1 mb-8">{{ 'catalogo.subtitulo' | transloco }}</p>
    <div class="space-y-12">
      <app-proveedores-seccion />
      <app-productos-seccion />
    </div>
  `,
})
export class Catalogo {}
