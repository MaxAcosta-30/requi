import { describe, expect, it } from 'vitest';
import { usuarioDesdeClaims } from './sesion';

describe('usuarioDesdeClaims', () => {
  it('lee nombre, correo y un rol suelto', () => {
    expect(usuarioDesdeClaims({ sub: '1', name: 'Ana', email: 'a@r.test', role: 'Compras' })).toEqual({
      id: '1',
      nombre: 'Ana',
      correo: 'a@r.test',
      roles: ['Compras'],
    });
  });

  it('acepta varios roles y descarta los desconocidos', () => {
    expect(usuarioDesdeClaims({ sub: '1', role: ['Aprobador', 'Admin', 'Solicitante'] })?.roles).toEqual([
      'Aprobador',
      'Solicitante',
    ]);
  });

  it('sin sub no hay usuario', () => {
    expect(usuarioDesdeClaims({ name: 'Ana' })).toBeNull();
    expect(usuarioDesdeClaims(null)).toBeNull();
  });

  it('sin claim de rol el usuario queda sin roles', () => {
    expect(usuarioDesdeClaims({ sub: '1' })?.roles).toEqual([]);
  });
});
