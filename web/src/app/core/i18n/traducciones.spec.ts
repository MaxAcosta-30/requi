import { describe, expect, it } from 'vitest';
import en from '../../../../public/i18n/en.json';
import es from '../../../../public/i18n/es.json';

/** Caso 8: el CI falla si una traducción existe en un idioma y falta en el otro. */
function claves(obj: object, prefijo = ''): string[] {
  return Object.entries(obj).flatMap(([k, v]) =>
    v !== null && typeof v === 'object' ? claves(v as object, `${prefijo}${k}.`) : [`${prefijo}${k}`],
  );
}

describe('traducciones', () => {
  it('español e inglés tienen exactamente las mismas claves', () => {
    const enEs = new Set(claves(es));
    const enEn = new Set(claves(en));
    expect([...enEs].filter((c) => !enEn.has(c)), 'faltan en inglés').toEqual([]);
    expect([...enEn].filter((c) => !enEs.has(c)), 'faltan en español').toEqual([]);
  });

  it('ningún texto está vacío', () => {
    const vacios = (obj: object): string[] =>
      claves(obj).filter((c) => !String(c.split('.').reduce<unknown>((o, k) => (o as Record<string, unknown>)[k], obj)).trim());
    expect(vacios(es)).toEqual([]);
    expect(vacios(en)).toEqual([]);
  });

  it('los parámetros {{x}} coinciden entre idiomas', () => {
    const valor = (obj: object, c: string) => String(c.split('.').reduce<unknown>((o, k) => (o as Record<string, unknown>)[k], obj));
    const params = (t: string) => [...t.matchAll(/\{\{\s*(\w+)\s*\}\}/g)].map((m) => m[1]).sort().join(',');
    const distintos = claves(es).filter((c) => params(valor(es, c)) !== params(valor(en, c)));
    expect(distintos).toEqual([]);
  });
});
