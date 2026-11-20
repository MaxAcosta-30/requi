import { Routes } from '@angular/router';

// /callback no necesita componente propio: la app canjea el código al revisar la sesión
// y luego regresa a la raíz.
export const routes: Routes = [
  { path: 'callback', children: [] },
  { path: '**', redirectTo: '' },
];
