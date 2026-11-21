import { FormArray, FormBuilder, Validators } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { FormRenglon, sinProductosRepetidos } from './nueva';

const fb = new FormBuilder().nonNullable;
const renglon = (productoId: string, cantidad: number): FormRenglon =>
  fb.group({
    productoId: fb.control(productoId, Validators.required),
    cantidad: fb.control(cantidad, [Validators.required, Validators.min(1), Validators.max(100_000), Validators.pattern(/^\d+$/)]),
  });

describe('formulario de renglones', () => {
  it('cantidad 0, negativa o con decimales es inválida', () => {
    for (const c of [0, -3, 2.5]) expect(renglon('p1', c).controls.cantidad.invalid, String(c)).toBe(true);
    expect(renglon('p1', 3).valid).toBe(true);
  });

  it('un renglón sin producto es inválido', () => {
    expect(renglon('', 1).invalid).toBe(true);
  });

  it('no permite el mismo producto en dos renglones', () => {
    const arr = new FormArray<FormRenglon>([renglon('p1', 1), renglon('p1', 2)], sinProductosRepetidos);
    expect(arr.hasError('repetido')).toBe(true);
    arr.at(1).controls.productoId.setValue('p2');
    expect(arr.hasError('repetido')).toBe(false);
  });

  it('los renglones aún vacíos no cuentan como repetidos', () => {
    const arr = new FormArray<FormRenglon>([renglon('', 1), renglon('', 1)], sinProductosRepetidos);
    expect(arr.hasError('repetido')).toBe(false);
  });
});
