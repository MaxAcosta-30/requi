import { defineConfig, devices } from '@playwright/test';

const enCI = !!process.env.CI;

export default defineConfig({
  testDir: '.',
  timeout: 120_000,
  retries: enCI ? 1 : 0,
  workers: 1,
  reporter: enCI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
    locale: 'es-MX',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  // levantar.sh aplica la base, arranca la API (5103) y por último Angular (4200).
  webServer: {
    command: './levantar.sh',
    url: 'http://localhost:4200',
    reuseExistingServer: !enCI,
    timeout: 300_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
