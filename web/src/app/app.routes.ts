import { Routes } from '@angular/router';

// La API decide los permisos; las rutas solo organizan la interfaz.
export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/requisiciones/lista').then((m) => m.Lista), data: { vista: 'mias' } },
  { path: 'por-aprobar', loadComponent: () => import('./features/requisiciones/lista').then((m) => m.Lista), data: { vista: 'por-aprobar' } },
  { path: 'todas', loadComponent: () => import('./features/requisiciones/lista').then((m) => m.Lista), data: { vista: 'todas' } },
  { path: 'nueva', loadComponent: () => import('./features/requisiciones/nueva').then((m) => m.Nueva) },
  { path: 'requisiciones/:id', loadComponent: () => import('./features/requisiciones/detalle').then((m) => m.Detalle) },
  { path: 'compras/catalogo', loadComponent: () => import('./features/compras/catalogo').then((m) => m.Catalogo) },
  { path: 'compras/ordenes', loadComponent: () => import('./features/compras/ordenes').then((m) => m.Ordenes) },
  { path: 'compras/avisos', loadComponent: () => import('./features/compras/avisos').then((m) => m.Avisos) },
  { path: 'compras/mensajes', loadComponent: () => import('./features/compras/mensajes').then((m) => m.Mensajes) },
  // /callback no necesita componente: la app canjea el código al revisar la sesión y vuelve a la raíz.
  { path: 'callback', children: [] },
  { path: '**', redirectTo: '' },
];
