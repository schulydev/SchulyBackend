import { defineConfig, devices } from '@playwright/test';

// The app is served by `ng serve`; every /api and /auth call is mocked with page.route() in the tests,
// so no backend is needed.
export default defineConfig({
  testDir: './e2e',
  globalTimeout: 600_000,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  workers: 3,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4300',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'bun run start -- --port 4300',
    url: 'http://localhost:4300',
    reuseExistingServer: true,
    timeout: 240_000,
  },
});
