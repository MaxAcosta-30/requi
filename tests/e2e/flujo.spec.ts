import { Browser, expect, Page, test } from '@playwright/test';

const clave = process.env.E2E_CLAVE ?? '';

/** Sesión nueva por usuario: contexto propio, login en la página de la API (:5103) y regreso a Angular. */
async function entrar(browser: Browser, correo: string): Promise<Page> {
  const contexto = await browser.newContext({ locale: 'es-MX' });
  const page = await contexto.newPage();
  await page.goto('/');
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();
  await page.waitForURL(/localhost:5103\/cuenta\/login/);
  await page.getByLabel('Correo').fill(correo);
  await page.getByLabel('Contraseña').fill(clave);
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();
  await expect(page.getByRole('button', { name: 'Salir' })).toBeVisible({ timeout: 30_000 });
  return page;
}

async function aprobar(browser: Browser, correo: string, folio: string): Promise<void> {
  const page = await entrar(browser, correo);
  await page.getByRole('link', { name: 'Por aprobar' }).click();
  const fila = page.getByRole('listitem').filter({ hasText: folio });
  await fila.getByRole('link', { name: 'Papelera E2E' }).click();
  await page.getByRole('button', { name: 'Aprobar y firmar' }).click();
  await expect(page.getByText('Ya registraste tu decisión.')).toBeVisible();
  await page.context().close();
}

test('solicitante crea, dos aprobadores firman y Compras ve la orden y el aviso', async ({ browser }) => {
  test.skip(!clave, 'Falta E2E_CLAVE');

  // 1. Solicitante: 2 renglones, $20,500 (arriba de $20,000 = 2 firmas).
  const sol = await entrar(browser, 'solicitante@demo.requi');
  await sol.getByRole('link', { name: 'Nueva requisición' }).first().click();
  await sol.locator('select[formcontrolname="proveedorId"]').selectOption({ label: 'Papelera E2E' });
  await sol.getByLabel('Producto 1').selectOption({ label: 'Impresora láser E2E' });
  await sol.getByRole('button', { name: 'Agregar renglón' }).click();
  await sol.getByLabel('Producto 2').selectOption({ label: 'Escritorio ejecutivo E2E' });
  await expect(sol.getByText('Necesitará 2 firmas')).toBeVisible();
  await sol.getByRole('button', { name: 'Enviar a firma' }).click();
  const aviso = await sol.getByRole('status').filter({ hasText: 'enviada' }).innerText();
  const folio = /REQ-\d{6}/.exec(aviso)?.[0];
  expect(folio, 'folio en el aviso de envío').toBeTruthy();
  const requisicionId = await (async () => {
    await sol.getByRole('listitem').filter({ hasText: folio! }).getByRole('link', { name: 'Papelera E2E' }).click();
    await sol.waitForURL(/\/requisiciones\/[0-9a-f-]{36}$/);
    return sol.url().split('/').pop()!;
  })();
  await expect(sol.getByText('En firma').first()).toBeVisible();
  await sol.context().close();

  // 2. Dos aprobadores distintos.
  await aprobar(browser, 'aprobador@demo.requi', folio!);
  await aprobar(browser, 'aprobador2@demo.requi', folio!);

  // 3. Compras: el Outbox (cada 5 s) crea la orden y luego el aviso.
  const compras = await entrar(browser, 'compras@demo.requi');
  await compras.goto('/compras/ordenes');
  const orden = compras.getByRole('listitem').filter({ has: compras.locator(`a[href="/requisiciones/${requisicionId}"]`) });
  await expect(async () => {
    await compras.reload();
    await expect(orden).toBeVisible({ timeout: 3_000 });
  }).toPass({ timeout: 30_000, intervals: [2_000] });
  const oc = /OC-\d{6}/.exec(await orden.innerText())?.[0];
  expect(oc, 'folio de la orden').toBeTruthy();
  await expect(orden).toContainText('$20,500.00');

  await compras.goto('/compras/avisos');
  const avisoProveedor = compras.getByRole('listitem').filter({ hasText: `Orden de compra ${oc}` });
  await expect(async () => {
    await compras.reload();
    await expect(avisoProveedor).toBeVisible({ timeout: 3_000 });
  }).toPass({ timeout: 30_000, intervals: [2_000] });
  await expect(avisoProveedor).toContainText('Papelera E2E');
  await compras.context().close();
});
