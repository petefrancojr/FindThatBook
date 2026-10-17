import { defineConfig, devices } from '@playwright/test';

// End-to-end tests run against the built site. In Docker that is the `web` service; set BASE_URL to point elsewhere.
// The browser opens in a window and every action is slowed down so a person can follow it. Set HEADLESS=true and
// SLOW_MO=0 to run without a window at full speed.
const headless = process.env['HEADLESS'] === 'true';

export default defineConfig({
  testDir: './e2e',
  forbidOnly: !!process.env['CI'],
  reporter: 'list',
  // One window at a time, so tests don't open on top of each other.
  workers: headless ? undefined : 1,
  // Slowed-down actions take longer than the default 30 seconds allows.
  timeout: 120_000,
  use: {
    baseURL: process.env['BASE_URL'] ?? 'http://localhost:4200',
    headless,
    launchOptions: {
      // Milliseconds Playwright waits between actions: clicks, typing, navigation.
      slowMo: Number(process.env['SLOW_MO'] ?? (headless ? 0 : 800)),
    },
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
